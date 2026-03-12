// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

namespace Allow2.Service;

/// <summary>
/// Interface for caching permission check results.
/// Simple key-value cache with TTL support. Values are serialized strings.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Retrieve a cached value by key.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cached value, or null if not found or expired.</returns>
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Store a value in cache.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="value">Value to cache.</param>
    /// <param name="ttlSeconds">Time-to-live in seconds (default 60).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetAsync(string key, string value, int ttlSeconds = 60, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a cached value.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
