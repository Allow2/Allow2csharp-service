// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

using System.Text.Json;
using Allow2.Service.Models;

namespace Allow2.Service.Storage;

/// <summary>
/// File-based JSON token storage.
///
/// Stores all tokens in a single JSON file. Suitable for development
/// and single-server deployments. Not recommended for high-concurrency
/// production use.
/// </summary>
public sealed class FileTokenStorage : ITokenStorage
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Dictionary<string, Dictionary<string, object>>? _data;

    /// <summary>
    /// Create a new file-based token storage.
    /// </summary>
    /// <param name="filePath">Path to the JSON storage file.</param>
    public FileTokenStorage(string filePath)
    {
        _filePath = filePath;
    }

    /// <inheritdoc />
    public async Task StoreAsync(string userId, OAuthTokens tokens, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var data = await LoadAllAsync(cancellationToken).ConfigureAwait(false);
            data[userId] = tokens.ToDictionary();
            await SaveAllAsync(data, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<OAuthTokens?> RetrieveAsync(string userId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var data = await LoadAllAsync(cancellationToken).ConfigureAwait(false);
            if (!data.TryGetValue(userId, out var tokenData))
            {
                return null;
            }
            return OAuthTokens.FromDictionary(tokenData);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string userId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var data = await LoadAllAsync(cancellationToken).ConfigureAwait(false);
            data.Remove(userId);
            await SaveAllAsync(data, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string userId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var data = await LoadAllAsync(cancellationToken).ConfigureAwait(false);
            return data.ContainsKey(userId);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Load all tokens from disk.
    /// </summary>
    private async Task<Dictionary<string, Dictionary<string, object>>> LoadAllAsync(
        CancellationToken cancellationToken)
    {
        if (_data != null)
        {
            return _data;
        }

        if (!File.Exists(_filePath))
        {
            _data = new Dictionary<string, Dictionary<string, object>>();
            return _data;
        }

        try
        {
            var contents = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
            var element = JsonSerializer.Deserialize<JsonElement>(contents);
            _data = new Dictionary<string, Dictionary<string, object>>();

            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    var innerDict = new Dictionary<string, object>();
                    if (property.Value.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var inner in property.Value.EnumerateObject())
                        {
                            innerDict[inner.Name] = HttpResponse.ConvertJsonElement(inner.Value);
                        }
                    }
                    _data[property.Name] = innerDict;
                }
            }
        }
        catch
        {
            _data = new Dictionary<string, Dictionary<string, object>>();
        }

        return _data;
    }

    /// <summary>
    /// Persist all tokens to disk.
    /// </summary>
    private async Task SaveAllAsync(
        Dictionary<string, Dictionary<string, object>> data,
        CancellationToken cancellationToken)
    {
        _data = data;

        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_filePath, json, cancellationToken).ConfigureAwait(false);
    }
}
