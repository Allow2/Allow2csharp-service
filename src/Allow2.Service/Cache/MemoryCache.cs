// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

using System.Collections.Concurrent;

namespace Allow2.Service.Cache;

/// <summary>
/// In-memory cache using <see cref="ConcurrentDictionary{TKey,TValue}"/> with TTL support.
///
/// No persistence between application restarts. Useful for testing or when
/// you only need to deduplicate checks within a single process lifecycle.
/// </summary>
public sealed class MemoryCache : ICacheService
{
    private readonly ConcurrentDictionary<string, (string Value, long ExpiresAt)> _store = new();

    /// <inheritdoc />
    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!_store.TryGetValue(key, out var entry))
        {
            return Task.FromResult<string?>(null);
        }

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= entry.ExpiresAt)
        {
            _store.TryRemove(key, out _);
            return Task.FromResult<string?>(null);
        }

        return Task.FromResult<string?>(entry.Value);
    }

    /// <inheritdoc />
    public Task SetAsync(string key, string value, int ttlSeconds = 60,
        CancellationToken cancellationToken = default)
    {
        var expiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ttlSeconds;
        _store[key] = (value, expiresAt);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
