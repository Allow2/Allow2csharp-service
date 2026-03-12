// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

using Allow2.Service.Models;

namespace Allow2.Service;

/// <summary>
/// Interface for per-user OAuth2 token persistence.
/// Implementations store tokens keyed by the application's internal user ID.
/// The user ID is an opaque string from the integrating application -- not
/// an Allow2 user ID.
/// </summary>
public interface ITokenStorage
{
    /// <summary>
    /// Store tokens for a user.
    /// </summary>
    Task StoreAsync(string userId, OAuthTokens tokens, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve tokens for a user, or null if none stored.
    /// </summary>
    Task<OAuthTokens?> RetrieveAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete tokens for a user (e.g., on unpair).
    /// </summary>
    Task DeleteAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check whether tokens exist for a user.
    /// </summary>
    Task<bool> ExistsAsync(string userId, CancellationToken cancellationToken = default);
}
