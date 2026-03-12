// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

using System.Web;
using Allow2.Service.Exceptions;
using Allow2.Service.Models;

namespace Allow2.Service;

/// <summary>
/// Handles the OAuth2 authorization flow for Allow2 Service API integrations.
///
/// Flow:
/// 1. Redirect user to <see cref="GetAuthorizeUrl"/>
/// 2. User links their Allow2 child account
/// 3. Allow2 redirects back with an authorization code
/// 4. Call <see cref="ExchangeCodeAsync"/> to get access + refresh tokens
/// 5. Tokens auto-refresh via <see cref="RefreshTokensAsync"/> when needed
/// </summary>
internal sealed class OAuth2Manager
{
    /// <summary>
    /// Seconds before actual expiry to trigger a refresh.
    /// </summary>
    private const int RefreshBuffer = 300;

    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly ITokenStorage _tokenStorage;
    private readonly IHttpClient _httpClient;
    private readonly string _apiHost;

    public OAuth2Manager(
        string clientId,
        string clientSecret,
        ITokenStorage tokenStorage,
        IHttpClient httpClient,
        string apiHost)
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
        _tokenStorage = tokenStorage;
        _httpClient = httpClient;
        _apiHost = apiHost;
    }

    /// <summary>
    /// Build the OAuth2 authorization URL to redirect the user to.
    /// </summary>
    /// <param name="userId">The integrating application's user ID.</param>
    /// <param name="redirectUri">Where Allow2 should redirect after authorization.</param>
    /// <param name="state">Optional CSRF state parameter.</param>
    /// <returns>The full authorization URL.</returns>
    public string GetAuthorizeUrl(string userId, string redirectUri, string? state = null)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["response_type"] = "code";
        query["client_id"] = _clientId;
        query["redirect_uri"] = redirectUri;
        query["user_id"] = userId;

        if (state != null)
        {
            query["state"] = state;
        }

        return $"{_apiHost}/oauth2/authorize?{query}";
    }

    /// <summary>
    /// Exchange an authorization code for access and refresh tokens.
    /// </summary>
    /// <param name="userId">The integrating application's user ID.</param>
    /// <param name="code">The authorization code from the callback.</param>
    /// <param name="redirectUri">Must match the redirect_uri used in GetAuthorizeUrl.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The token set.</returns>
    /// <exception cref="ApiException">If the token exchange fails.</exception>
    public async Task<OAuthTokens> ExchangeCodeAsync(
        string userId,
        string code,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync(
            $"{_apiHost}/oauth2/token",
            new Dictionary<string, object>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccess)
        {
            Dictionary<string, object>? body = null;
            try { body = response.JsonAsync(); } catch { /* ignore parse errors */ }
            var errorDesc = body != null && body.TryGetValue("error_description", out var desc)
                ? desc.ToString()
                : response.Body;
            throw new ApiException(
                message: $"OAuth2 token exchange failed: {errorDesc}",
                httpStatusCode: response.StatusCode,
                responseBody: body);
        }

        var tokens = OAuthTokens.FromApiResponse(response.JsonAsync());
        await _tokenStorage.StoreAsync(userId, tokens, cancellationToken).ConfigureAwait(false);

        return tokens;
    }

    /// <summary>
    /// Get a valid access token for the user, refreshing if necessary.
    /// </summary>
    /// <param name="userId">The integrating application's user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A valid access token.</returns>
    /// <exception cref="TokenExpiredException">If no tokens exist or refresh fails.</exception>
    public async Task<string> GetAccessTokenAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var tokens = await _tokenStorage.RetrieveAsync(userId, cancellationToken).ConfigureAwait(false);

        if (tokens == null)
        {
            throw new TokenExpiredException(userId, "No tokens stored for user. Authorization required.");
        }

        if (!tokens.IsExpired(RefreshBuffer))
        {
            return tokens.AccessToken;
        }

        var refreshed = await RefreshTokensAsync(userId, tokens, cancellationToken).ConfigureAwait(false);
        return refreshed.AccessToken;
    }

    /// <summary>
    /// Refresh the OAuth2 tokens using the refresh token.
    /// </summary>
    /// <param name="userId">The integrating application's user ID.</param>
    /// <param name="tokens">The current (expired) token set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The refreshed token set.</returns>
    /// <exception cref="TokenExpiredException">If the refresh fails.</exception>
    public async Task<OAuthTokens> RefreshTokensAsync(
        string userId,
        OAuthTokens tokens,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync(
            $"{_apiHost}/oauth2/token",
            new Dictionary<string, object>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = tokens.RefreshToken,
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccess)
        {
            await _tokenStorage.DeleteAsync(userId, cancellationToken).ConfigureAwait(false);
            throw new TokenExpiredException(
                userId: userId,
                message: "OAuth2 token refresh failed. Re-authorization required.",
                innerException: new ApiException(
                    message: $"Refresh token request returned HTTP {response.StatusCode}",
                    httpStatusCode: response.StatusCode));
        }

        var newTokens = OAuthTokens.FromApiResponse(response.JsonAsync());
        await _tokenStorage.StoreAsync(userId, newTokens, cancellationToken).ConfigureAwait(false);

        return newTokens;
    }

    /// <summary>
    /// Check whether a user's service account pairing is still valid.
    /// </summary>
    /// <param name="userId">The integrating application's user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if still paired, false otherwise.</returns>
    public async Task<bool> CheckPairingStatusAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (!await _tokenStorage.ExistsAsync(userId, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        string accessToken;
        try
        {
            accessToken = await GetAccessTokenAsync(userId, cancellationToken).ConfigureAwait(false);
        }
        catch (TokenExpiredException)
        {
            return false;
        }

        var response = await _httpClient.PostAsync(
            $"{_apiHost}/oauth2/checkStatus",
            new Dictionary<string, object>
            {
                ["access_token"] = accessToken,
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccess)
        {
            return false;
        }

        try
        {
            var data = response.JsonAsync();
            if (data.TryGetValue("paired", out var paired))
            {
                return Convert.ToBoolean(paired);
            }
            if (data.TryGetValue("active", out var active))
            {
                return Convert.ToBoolean(active);
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Unpair a user by deleting their stored tokens.
    /// </summary>
    public async Task UnpairAsync(string userId, CancellationToken cancellationToken = default)
    {
        await _tokenStorage.DeleteAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Check whether tokens exist for the given user.
    /// </summary>
    public async Task<bool> HasTokensAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _tokenStorage.ExistsAsync(userId, cancellationToken).ConfigureAwait(false);
    }
}
