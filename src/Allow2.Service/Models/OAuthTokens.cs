// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

namespace Allow2.Service.Models;

/// <summary>
/// Value object representing an OAuth2 token set.
/// </summary>
public sealed class OAuthTokens
{
    /// <summary>The OAuth2 access token.</summary>
    public string AccessToken { get; }

    /// <summary>The OAuth2 refresh token.</summary>
    public string RefreshToken { get; }

    /// <summary>Unix timestamp when the access token expires.</summary>
    public long ExpiresAt { get; }

    public OAuthTokens(string accessToken, string refreshToken, long expiresAt)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// Check whether the access token has expired or is about to expire.
    /// </summary>
    /// <param name="bufferSeconds">Seconds before actual expiry to consider "expired" (default 300 = 5 min).</param>
    public bool IsExpired(int bufferSeconds = 300)
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= (ExpiresAt - bufferSeconds);
    }

    /// <summary>
    /// Create from an API token response.
    /// </summary>
    public static OAuthTokens FromApiResponse(Dictionary<string, object> response)
    {
        return new OAuthTokens(
            accessToken: response["access_token"].ToString()!,
            refreshToken: response["refresh_token"].ToString()!,
            expiresAt: DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Convert.ToInt64(response["expires_in"]));
    }

    /// <summary>
    /// Serialize to dictionary for storage.
    /// </summary>
    public Dictionary<string, object> ToDictionary()
    {
        return new Dictionary<string, object>
        {
            ["access_token"] = AccessToken,
            ["refresh_token"] = RefreshToken,
            ["expires_at"] = ExpiresAt,
        };
    }

    /// <summary>
    /// Reconstitute from stored dictionary.
    /// </summary>
    public static OAuthTokens FromDictionary(Dictionary<string, object> data)
    {
        return new OAuthTokens(
            accessToken: data["access_token"].ToString()!,
            refreshToken: data["refresh_token"].ToString()!,
            expiresAt: Convert.ToInt64(data["expires_at"]));
    }
}
