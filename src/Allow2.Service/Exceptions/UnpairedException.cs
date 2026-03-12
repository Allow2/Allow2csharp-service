// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

namespace Allow2.Service.Exceptions;

/// <summary>
/// Thrown when the service account is no longer linked (HTTP 401/403 from API).
/// The integration should redirect the user through OAuth2 re-pairing.
/// </summary>
public class UnpairedException : Allow2Exception
{
    /// <summary>The user ID whose pairing was lost.</summary>
    public string UserId { get; }

    public UnpairedException(
        string userId,
        string message = "Service account is no longer linked. Re-pairing required.",
        Exception? innerException = null)
        : base(
            message,
            innerException,
            new Dictionary<string, object?> { ["userId"] = userId })
    {
        UserId = userId;
    }
}
