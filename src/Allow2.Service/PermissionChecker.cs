// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

using System.Text.Json;
using Allow2.Service.Exceptions;
using Allow2.Service.Models;

namespace Allow2.Service;

/// <summary>
/// Checks child permissions via the Allow2 Service API.
///
/// Results are cached per user for a configurable TTL to avoid
/// excessive API calls during a single page load or request cycle.
/// </summary>
internal sealed class PermissionChecker
{
    private const int DefaultCacheTtl = 60;

    private readonly IHttpClient _httpClient;
    private readonly ICacheService _cache;
    private readonly string _serviceHost;
    private readonly int _cacheTtl;

    public PermissionChecker(
        IHttpClient httpClient,
        ICacheService cache,
        string serviceHost,
        int cacheTtl = DefaultCacheTtl)
    {
        _httpClient = httpClient;
        _cache = cache;
        _serviceHost = serviceHost;
        _cacheTtl = cacheTtl;
    }

    /// <summary>
    /// Check permissions for a user's linked child account.
    ///
    /// Activities can be specified in several formats:
    ///
    /// 1. Array of dictionaries: [new Dictionary { ["id"] = 1, ["log"] = true }]
    /// 2. Simple list of activity IDs: [1, 3, 8]
    /// 3. Legacy dictionary format: { { 1, 1 }, { 3, 1 } }
    /// </summary>
    /// <param name="accessToken">Valid OAuth2 access token.</param>
    /// <param name="userId">The integrating application's user ID (for cache keying).</param>
    /// <param name="activities">Activity IDs to check (see format options above).</param>
    /// <param name="timezone">IANA timezone (e.g., "Australia/Brisbane"). Null for server default.</param>
    /// <param name="log">Whether to log usage (default true).</param>
    /// <param name="useCache">Whether to use cached results (default true).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The permission check result.</returns>
    /// <exception cref="UnpairedException">If the API returns 401/403 (account unlinked).</exception>
    /// <exception cref="ApiException">On other API failures.</exception>
    public async Task<CheckResult> CheckAsync(
        string accessToken,
        string userId,
        List<object> activities,
        string? timezone = null,
        bool log = true,
        bool useCache = true,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeActivities(activities);

        // Check cache first
        if (useCache)
        {
            var cacheKey = BuildCacheKey(userId, normalized);
            var cached = await _cache.GetAsync(cacheKey, cancellationToken).ConfigureAwait(false);

            if (cached != null)
            {
                try
                {
                    var element = JsonSerializer.Deserialize<JsonElement>(cached);
                    var data = HttpResponse.JsonElementToDictionary(element);
                    return CheckResult.FromApiResponse(data);
                }
                catch (JsonException)
                {
                    await _cache.DeleteAsync(cacheKey, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        var payload = new Dictionary<string, object>
        {
            ["access_token"] = accessToken,
            ["activities"] = normalized,
            ["log"] = log,
        };

        if (timezone != null)
        {
            payload["tz"] = timezone;
        }

        var response = await _httpClient.PostAsync(
            $"{_serviceHost}/serviceapi/check",
            payload,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // 401/403 = unpaired
        if (response.IsUnauthorized)
        {
            throw new UnpairedException(userId);
        }

        if (!response.IsSuccess)
        {
            Dictionary<string, object>? body = null;
            try { body = response.JsonAsync(); } catch { /* ignore parse errors */ }
            throw new ApiException(
                message: $"Permission check failed: HTTP {response.StatusCode}",
                httpStatusCode: response.StatusCode,
                responseBody: body);
        }

        var responseData = response.JsonAsync();
        var result = CheckResult.FromApiResponse(responseData);

        // Cache the raw response
        if (useCache)
        {
            var cacheKey = BuildCacheKey(userId, normalized);
            await _cache.SetAsync(cacheKey, response.Body, _cacheTtl, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// Convenience method: check if specific activities are all allowed.
    /// </summary>
    /// <param name="accessToken">Valid OAuth2 access token.</param>
    /// <param name="userId">The integrating application's user ID.</param>
    /// <param name="activityIds">Activity IDs to check.</param>
    /// <param name="timezone">IANA timezone.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if all specified activities are allowed.</returns>
    /// <exception cref="UnpairedException">If the API returns 401/403.</exception>
    /// <exception cref="ApiException">On other API failures.</exception>
    public async Task<bool> IsAllowedAsync(
        string accessToken,
        string userId,
        List<int> activityIds,
        string? timezone = null,
        CancellationToken cancellationToken = default)
    {
        // Accept plain IDs -- NormalizeActivities in CheckAsync handles the conversion
        var activities = activityIds.Cast<object>().ToList();
        var result = await CheckAsync(accessToken, userId, activities, timezone, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        foreach (var activity in result.Activities)
        {
            if (!activity.Allowed)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Invalidate the cached check result for a user.
    /// </summary>
    /// <param name="userId">The integrating application's user ID.</param>
    /// <param name="activities">The same activities list used in check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task InvalidateCacheAsync(
        string userId,
        List<object> activities,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeActivities(activities);
        var cacheKey = BuildCacheKey(userId, normalized);
        await _cache.DeleteAsync(cacheKey, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Normalize activities into the standard list-of-dictionaries format.
    ///
    /// Accepts:
    /// - List of Dictionary with "id" and "log" keys -- passed through as-is
    /// - List of ints [1, 3, 8] -- expanded to dictionaries with log=true
    /// - Dictionary of int to int {1: 1, 3: 1} -- legacy format, converted
    /// </summary>
    internal static List<Dictionary<string, object>> NormalizeActivities(List<object> activities)
    {
        if (activities.Count == 0)
        {
            return new List<Dictionary<string, object>>();
        }

        var first = activities[0];

        // Format 1: already a dictionary with "id" key
        if (first is Dictionary<string, object> dict && dict.ContainsKey("id"))
        {
            return activities
                .OfType<Dictionary<string, object>>()
                .ToList();
        }

        // Format 2: simple list of integer IDs
        var normalized = new List<Dictionary<string, object>>();
        foreach (var item in activities)
        {
            var id = Convert.ToInt32(item);
            normalized.Add(new Dictionary<string, object>
            {
                ["id"] = id,
                ["log"] = true,
            });
        }

        return normalized;
    }

    /// <summary>
    /// Build a deterministic cache key for a user + activities combination.
    /// </summary>
    private static string BuildCacheKey(string userId, List<Dictionary<string, object>> activities)
    {
        var ids = activities
            .Select(a => Convert.ToInt32(a["id"]))
            .OrderBy(id => id)
            .ToList();
        var actSuffix = string.Join("_", ids);

        return $"allow2_check_{userId}_{actSuffix}";
    }
}
