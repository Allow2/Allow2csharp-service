// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

using System.Collections.Concurrent;
using Allow2.Service.Models;

namespace Allow2.Service.Storage;

/// <summary>
/// In-memory token storage using <see cref="ConcurrentDictionary{TKey,TValue}"/>.
///
/// No persistence between application restarts. Useful for testing or
/// short-lived processes.
/// </summary>
public sealed class MemoryTokenStorage : ITokenStorage
{
    private readonly ConcurrentDictionary<string, OAuthTokens> _store = new();

    /// <inheritdoc />
    public Task StoreAsync(string userId, OAuthTokens tokens, CancellationToken cancellationToken = default)
    {
        _store[userId] = tokens;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<OAuthTokens?> RetrieveAsync(string userId, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(userId, out var tokens);
        return Task.FromResult(tokens);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string userId, CancellationToken cancellationToken = default)
    {
        _store.TryRemove(userId, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.ContainsKey(userId));
    }
}
