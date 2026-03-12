// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

namespace Allow2.Service.Exceptions;

/// <summary>
/// Thrown when the OAuth2 token refresh fails and no valid token is available.
/// </summary>
public class TokenExpiredException : Allow2Exception
{
    /// <summary>The user ID whose tokens expired.</summary>
    public string UserId { get; }

    public TokenExpiredException(
        string userId,
        string message = "OAuth2 token refresh failed. Re-authorization required.",
        Exception? innerException = null)
        : base(
            message,
            innerException,
            new Dictionary<string, object?> { ["userId"] = userId })
    {
        UserId = userId;
    }
}
