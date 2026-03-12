// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

using System.Text.Json;
using System.Text.RegularExpressions;

namespace Allow2.Service.Cache;

/// <summary>
/// File-based permission cache.
///
/// Each cache entry is stored as a separate JSON file in the cache directory.
/// Suitable for single-server deployments.
/// </summary>
public sealed class FileCache : ICacheService
{
    private readonly string _cacheDir;

    /// <summary>
    /// Create a new file-based cache.
    /// </summary>
    /// <param name="cacheDir">Directory for cache files. Created automatically if missing.</param>
    public FileCache(string cacheDir)
    {
        _cacheDir = cacheDir;

        if (!Directory.Exists(_cacheDir))
        {
            Directory.CreateDirectory(_cacheDir);
        }
    }

    /// <inheritdoc />
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = KeyToPath(key);

        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var contents = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            var entry = JsonSerializer.Deserialize<JsonElement>(contents);

            if (entry.ValueKind != JsonValueKind.Object)
            {
                TryDelete(path);
                return null;
            }

            if (!entry.TryGetProperty("expiresAt", out var expiresAt))
            {
                TryDelete(path);
                return null;
            }

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= expiresAt.GetInt64())
            {
                TryDelete(path);
                return null;
            }

            if (entry.TryGetProperty("value", out var value))
            {
                return value.GetString();
            }

            return null;
        }
        catch
        {
            TryDelete(path);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SetAsync(string key, string value, int ttlSeconds = 60,
        CancellationToken cancellationToken = default)
    {
        var path = KeyToPath(key);

        var entry = JsonSerializer.Serialize(new
        {
            value,
            expiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ttlSeconds,
        });

        await File.WriteAllTextAsync(path, entry, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = KeyToPath(key);
        TryDelete(path);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Convert a cache key to a safe filesystem path.
    /// </summary>
    private string KeyToPath(string key)
    {
        var safeKey = Regex.Replace(key, @"[^a-zA-Z0-9_\-]", "_");
        return Path.Combine(_cacheDir, safeKey + ".json");
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* ignore */ }
    }
}
