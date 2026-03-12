// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

using Allow2.Service.Exceptions;
using Allow2.Service.Models;

namespace Allow2.Service;

/// <summary>
/// Main entry point for the Allow2 C#/.NET Service SDK (Service API).
///
/// This is the facade that integrating applications interact with.
/// It wraps OAuth2 authorization, permission checking, request creation,
/// feedback, and offline voice code features into a single coherent API.
///
/// Each linked service account maps to exactly one Allow2 child -- there is
/// no child selector in Service API integrations.
/// </summary>
/// <example>
/// <code>
/// var client = new Allow2Client(
///     clientId: "your-service-token",
///     clientSecret: "your-service-secret",
///     tokenStorage: new FileTokenStorage("/var/lib/allow2/tokens.json"),
///     cache: new MemoryCache());
///
/// // Start OAuth flow
/// var url = client.GetAuthorizeUrl(userId, "https://example.com/callback");
///
/// // After callback
/// await client.ExchangeCodeAsync(userId, code, "https://example.com/callback");
///
/// // Check permissions
/// var result = await client.CheckAsync(userId, new List&lt;object&gt; { 1, 3 });
/// if (result.Allowed) { /* allow access */ }
/// </code>
/// </example>
/// <seealso href="https://developer.allow2.com">Allow2 Developer Portal</seealso>
public sealed class Allow2Client
{
    private readonly OAuth2Manager _oauth;
    private readonly PermissionChecker _checker;
    private readonly RequestManager _requests;
    private readonly FeedbackManager _feedback;
    private readonly ITokenStorage _tokenStorage;

    /// <summary>
    /// Create a new Allow2 Service API client.
    /// </summary>
    /// <param name="clientId">Service token from developer.allow2.com.</param>
    /// <param name="clientSecret">Service secret from developer.allow2.com.</param>
    /// <param name="tokenStorage">Per-user token persistence.</param>
    /// <param name="cache">Permission check result cache.</param>
    /// <param name="httpClient">Custom HTTP client (default: System.Net.Http.HttpClient-based).</param>
    /// <param name="apiHost">Allow2 API base URL.</param>
    /// <param name="serviceHost">Allow2 Service base URL.</param>
    /// <param name="cacheTtl">Permission check cache TTL in seconds (default 60).</param>
    public Allow2Client(
        string clientId,
        string clientSecret,
        ITokenStorage tokenStorage,
        ICacheService cache,
        IHttpClient? httpClient = null,
        string apiHost = "https://api.allow2.com",
        string serviceHost = "https://service.allow2.com",
        int cacheTtl = 60)
    {
        _tokenStorage = tokenStorage;
        var http = httpClient ?? new Allow2HttpClient();

        _oauth = new OAuth2Manager(
            clientId: clientId,
            clientSecret: clientSecret,
            tokenStorage: tokenStorage,
            httpClient: http,
            apiHost: apiHost);

        _checker = new PermissionChecker(
            httpClient: http,
            cache: cache,
            serviceHost: serviceHost,
            cacheTtl: cacheTtl);

        _requests = new RequestManager(
            httpClient: http,
            apiHost: apiHost);

        _feedback = new FeedbackManager(
            httpClient: http,
            apiHost: apiHost);
    }

    // ──────────────────────────────────────────────────────────
    // OAuth2
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Build the OAuth2 authorization URL to redirect the user to.
    /// The user will be asked to link their Allow2 child account to your service.
    /// </summary>
    /// <param name="userId">Your application's user ID.</param>
    /// <param name="redirectUri">Where Allow2 should redirect after authorization.</param>
    /// <param name="state">Optional CSRF state parameter (recommended).</param>
    /// <returns>The full authorization URL.</returns>
    public string GetAuthorizeUrl(string userId, string redirectUri, string? state = null)
    {
        return _oauth.GetAuthorizeUrl(userId, redirectUri, state);
    }

    /// <summary>
    /// Exchange an authorization code for OAuth2 tokens.
    /// Call this in your redirect_uri callback handler.
    /// </summary>
    /// <param name="userId">Your application's user ID.</param>
    /// <param name="code">The authorization code from the callback query string.</param>
    /// <param name="redirectUri">Must match the redirect_uri used in GetAuthorizeUrl.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The token set (also stored automatically).</returns>
    /// <exception cref="ApiException">If the token exchange fails.</exception>
    public async Task<OAuthTokens> ExchangeCodeAsync(
        string userId,
        string code,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        return await _oauth.ExchangeCodeAsync(userId, code, redirectUri, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Check whether a user's Allow2 account pairing is still valid.
    /// </summary>
    /// <param name="userId">Your application's user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the account is still linked.</returns>
    public async Task<bool> CheckPairingStatusAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _oauth.CheckPairingStatusAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Unpair a user by removing their stored OAuth2 tokens.
    /// </summary>
    /// <param name="userId">Your application's user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task UnpairAsync(string userId, CancellationToken cancellationToken = default)
    {
        await _oauth.UnpairAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Check whether the user has stored OAuth2 tokens (i.e., has been paired).
    /// </summary>
    /// <param name="userId">Your application's user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if tokens exist.</returns>
    public async Task<bool> IsPairedAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _oauth.HasTokensAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    // ──────────────────────────────────────────────────────────
    // Permission Checking
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Check permissions for a user's linked Allow2 child account.
    ///
    /// Activities can be specified in several formats:
    /// 1. List of dictionaries: [new Dictionary { ["id"] = 1, ["log"] = true }]
    /// 2. Simple list of activity IDs (auto-expanded with log=true): [1, 3, 8]
    ///
    /// Common activity IDs: 1 (Internet), 3 (Gaming), 6 (Social), 8 (Screen Time).
    /// </summary>
    /// <param name="userId">Your application's user ID.</param>
    /// <param name="activities">Activities to check (see format options above).</param>
    /// <param name="timezone">IANA timezone (e.g., "Australia/Brisbane").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detailed permission check result.</returns>
    /// <exception cref="UnpairedException">If the account is no longer linked.</exception>
    /// <exception cref="TokenExpiredException">If token refresh fails.</exception>
    /// <exception cref="ApiException">On API errors.</exception>
    public async Task<CheckResult> CheckAsync(
        string userId,
        List<object> activities,
        string? timezone = null,
        CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAccessTokenAsync(userId, cancellationToken).ConfigureAwait(false);

        try
        {
            return await _checker.CheckAsync(accessToken, userId, activities, timezone,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (UnpairedException)
        {
            await HandleUnpairedAsync(userId, cancellationToken).ConfigureAwait(false);
            throw; // unreachable -- HandleUnpairedAsync always throws
        }
    }

    /// <summary>
    /// Convenience: check if all specified activities are currently allowed.
    /// </summary>
    /// <param name="userId">Your application's user ID.</param>
    /// <param name="activityIds">Activity IDs to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if all activities are allowed.</returns>
    /// <exception cref="UnpairedException">If the account is no longer linked.</exception>
    /// <exception cref="TokenExpiredException">If token refresh fails.</exception>
    /// <exception cref="ApiException">On API errors.</exception>
    public async Task<bool> IsAllowedAsync(
        string userId,
        List<int> activityIds,
        CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAccessTokenAsync(userId, cancellationToken).ConfigureAwait(false);

        try
        {
            return await _checker.IsAllowedAsync(accessToken, userId, activityIds,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (UnpairedException)
        {
            await HandleUnpairedAsync(userId, cancellationToken).ConfigureAwait(false);
            throw; // unreachable
        }
    }

    // ──────────────────────────────────────────────────────────
    // Requests (More Time, Day Type Change, Ban Lift)
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Request more time for an activity.
    /// </summary>
    /// <param name="userId">Your application's user ID.</param>
    /// <param name="activityId">The activity to request more time for.</param>
    /// <param name="minutes">Number of additional minutes.</param>
    /// <param name="message">Optional message to the parent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Request ID and status secret for polling.</returns>
    /// <exception cref="UnpairedException">If the account is no longer linked.</exception>
    /// <exception cref="TokenExpiredException">If token refresh fails.</exception>
    /// <exception cref="ApiException">On API errors.</exception>
    public async Task<RequestResult> RequestMoreTimeAsync(
        string userId,
        int activityId,
        int minutes,
        string message = "",
        CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAccessTokenAsync(userId, cancellationToken).ConfigureAwait(false);

        try
        {
            return await _requests.RequestMoreTimeAsync(accessToken, userId, activityId, minutes, message,
                cancellationToken).ConfigureAwait(false);
        }
        catch (UnpairedException)
        {
            await HandleUnpairedAsync(userId, cancellationToken).ConfigureAwait(false);
            throw; // unreachable
        }
    }

    /// <summary>
    /// Request a day type change (e.g., treat today as a weekend).
    /// </summary>
    /// <param name="userId">Your application's user ID.</param>
    /// <param name="dayTypeId">The desired day type ID.</param>
    /// <param name="message">Optional message to the parent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Request ID and status secret for polling.</returns>
    /// <exception cref="UnpairedException">If the account is no longer linked.</exception>
    /// <exception cref="TokenExpiredException">If token refresh fails.</exception>
    /// <exception cref="ApiException">On API errors.</exception>
    public async Task<RequestResult> RequestDayTypeChangeAsync(
        string userId,
        int dayTypeId,
        string message = "",
        CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAccessTokenAsync(userId, cancellationToken).ConfigureAwait(false);

        try
        {
            return await _requests.RequestDayTypeChangeAsync(accessToken, userId, dayTypeId, message,
                cancellationToken).ConfigureAwait(false);
        }
        catch (UnpairedException)
        {
            await HandleUnpairedAsync(userId, cancellationToken).ConfigureAwait(false);
            throw; // unreachable
        }
    }

    /// <summary>
    /// Request lifting a ban on an activity.
    /// </summary>
    /// <param name="userId">Your application's user ID.</param>
    /// <param name="activityId">The banned activity.</param>
    /// <param name="message">Optional message to the parent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Request ID and status secret for polling.</returns>
    /// <exception cref="UnpairedException">If the account is no longer linked.</exception>
    /// <exception cref="TokenExpiredException">If token refresh fails.</exception>
    /// <exception cref="ApiException">On API errors.</exception>
    public async Task<RequestResult> RequestBanLiftAsync(
        string userId,
        int activityId,
        string message = "",
        CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAccessTokenAsync(userId, cancellationToken).ConfigureAwait(false);

        try
        {
            return await _requests.RequestBanLiftAsync(accessToken, userId, activityId, message,
                cancellationToken).ConfigureAwait(false);
        }
        catch (UnpairedException)
        {
            await HandleUnpairedAsync(userId, cancellationToken).ConfigureAwait(false);
            throw; // unreachable
        }
    }

    /// <summary>
    /// Poll the status of a pending request.
    /// </summary>
    /// <param name="requestId">The request ID from a create method.</param>
    /// <param name="statusSecret">The status secret from a create method.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current status: "pending", "approved", or "denied".</returns>
    /// <exception cref="ApiException">On API errors.</exception>
    public async Task<string> GetRequestStatusAsync(
        string requestId,
        string statusSecret,
        CancellationToken cancellationToken = default)
    {
        return await _requests.GetRequestStatusAsync(requestId, statusSecret, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Obtain a temporary request token from the Allow2 API.
    /// Used by integrations that manage the request creation flow themselves.
    /// </summary>
    /// <param name="userId">Your application's user ID.</param>
    /// <param name="nonce">Optional nonce for idempotency.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Raw response dictionary with token data.</returns>
    /// <exception cref="TokenExpiredException">If no tokens exist or refresh fails.</exception>
    /// <exception cref="ApiException">On API errors.</exception>
    public async Task<Dictionary<string, object>> GetRequestTokenAsync(
        string userId,
        string? nonce = null,
        CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAccessTokenAsync(userId, cancellationToken).ConfigureAwait(false);
        return await _requests.GetTempTokenAsync(accessToken, nonce, cancellationToken).ConfigureAwait(false);
    }

    // ──────────────────────────────────────────────────────────
    // Voice Codes (Offline Approval)
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Generate an offline voice code challenge-response pair.
    /// The challenge is displayed to the child, who reads it to the parent.
    /// The parent's Allow2 app computes the response; the child enters it.
    /// </summary>
    /// <param name="secret">The child's pairing secret. Must be provided by the integration.</param>
    /// <param name="type">The type of request.</param>
    /// <param name="activityId">The activity ID.</param>
    /// <param name="minutes">Minutes requested (for MoreTime type, ignored otherwise).</param>
    /// <param name="userId">Your application's user ID (reserved for future use).</param>
    /// <param name="date">Override date for testing (yyyy-MM-dd format).</param>
    /// <returns>Challenge to display and expected response.</returns>
    public VoiceCodePair GenerateVoiceChallenge(
        string secret,
        RequestType type,
        int activityId,
        int minutes = 0,
        string? userId = null,
        string? date = null)
    {
        if (string.IsNullOrEmpty(secret))
        {
            throw new ArgumentException(
                "A pairing secret is required for voice code generation. " +
                "This should be stored during the OAuth2 pairing process.",
                nameof(secret));
        }

        return VoiceCode.Generate(secret, type, activityId, minutes, date);
    }

    /// <summary>
    /// Verify a voice code response entered by the child.
    /// </summary>
    /// <param name="secret">The child's pairing secret.</param>
    /// <param name="challenge">The challenge code that was displayed.</param>
    /// <param name="response">The response code entered by the child.</param>
    /// <param name="userId">Your application's user ID (reserved for future use).</param>
    /// <param name="date">Override date for testing (yyyy-MM-dd format).</param>
    /// <returns>True if the response is valid.</returns>
    public bool VerifyVoiceResponse(
        string secret,
        string challenge,
        string response,
        string? userId = null,
        string? date = null)
    {
        if (string.IsNullOrEmpty(secret))
        {
            throw new ArgumentException(
                "A pairing secret is required for voice code verification.",
                nameof(secret));
        }

        return VoiceCode.Verify(secret, challenge, response, date);
    }

    // ──────────────────────────────────────────────────────────
    // Feedback
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Submit feedback from the user.
    /// </summary>
    /// <param name="userId">Your application's user ID.</param>
    /// <param name="category">Feedback category.</param>
    /// <param name="message">The feedback message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The discussion ID for the created thread.</returns>
    /// <exception cref="UnpairedException">If the account is no longer linked.</exception>
    /// <exception cref="TokenExpiredException">If token refresh fails.</exception>
    /// <exception cref="ApiException">On API errors.</exception>
    public async Task<string> SubmitFeedbackAsync(
        string userId,
        FeedbackCategory category,
        string message,
        CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAccessTokenAsync(userId, cancellationToken).ConfigureAwait(false);

        try
        {
            return await _feedback.SubmitAsync(accessToken, userId, category, message, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (UnpairedException)
        {
            await HandleUnpairedAsync(userId, cancellationToken).ConfigureAwait(false);
            throw; // unreachable
        }
    }

    /// <summary>
    /// Load all feedback discussion threads for the user.
    /// </summary>
    /// <param name="userId">Your application's user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of discussion threads.</returns>
    /// <exception cref="UnpairedException">If the account is no longer linked.</exception>
    /// <exception cref="TokenExpiredException">If token refresh fails.</exception>
    /// <exception cref="ApiException">On API errors.</exception>
    public async Task<List<object>> LoadFeedbackAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAccessTokenAsync(userId, cancellationToken).ConfigureAwait(false);

        try
        {
            return await _feedback.LoadAsync(accessToken, userId, cancellationToken).ConfigureAwait(false);
        }
        catch (UnpairedException)
        {
            await HandleUnpairedAsync(userId, cancellationToken).ConfigureAwait(false);
            throw; // unreachable
        }
    }

    /// <summary>
    /// Reply to an existing feedback discussion thread.
    /// </summary>
    /// <param name="userId">Your application's user ID.</param>
    /// <param name="discussionId">The discussion thread ID.</param>
    /// <param name="message">The reply message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="UnpairedException">If the account is no longer linked.</exception>
    /// <exception cref="TokenExpiredException">If token refresh fails.</exception>
    /// <exception cref="ApiException">On API errors.</exception>
    public async Task ReplyToFeedbackAsync(
        string userId,
        string discussionId,
        string message,
        CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAccessTokenAsync(userId, cancellationToken).ConfigureAwait(false);

        try
        {
            await _feedback.ReplyAsync(accessToken, userId, discussionId, message, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (UnpairedException)
        {
            await HandleUnpairedAsync(userId, cancellationToken).ConfigureAwait(false);
            throw; // unreachable
        }
    }

    // ──────────────────────────────────────────────────────────
    // Internal
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Get a valid access token for the user, auto-refreshing if needed.
    /// </summary>
    private async Task<string> GetAccessTokenAsync(string userId, CancellationToken cancellationToken)
    {
        return await _oauth.GetAccessTokenAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Handle an unpaired response: clear tokens and throw.
    /// </summary>
    private async Task HandleUnpairedAsync(string userId, CancellationToken cancellationToken)
    {
        await _tokenStorage.DeleteAsync(userId, cancellationToken).ConfigureAwait(false);
        throw new UnpairedException(userId);
    }
}
