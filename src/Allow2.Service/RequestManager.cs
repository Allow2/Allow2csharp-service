// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

using Allow2.Service.Exceptions;
using Allow2.Service.Models;

namespace Allow2.Service;

/// <summary>
/// Manages child requests (more time, day type change, ban lift) via the Allow2 Service API.
///
/// Flow:
/// 1. Obtain a temporary request token via /request/tempToken
/// 2. Create the request via /request/createRequest
/// 3. Poll status via /request/{id}/status using the statusSecret
/// </summary>
internal sealed class RequestManager
{
    private readonly IHttpClient _httpClient;
    private readonly string _apiHost;

    public RequestManager(IHttpClient httpClient, string apiHost)
    {
        _httpClient = httpClient;
        _apiHost = apiHost;
    }

    /// <summary>
    /// Request more time for an activity.
    /// </summary>
    /// <param name="accessToken">Valid OAuth2 access token.</param>
    /// <param name="userId">The integrating application's user ID.</param>
    /// <param name="activityId">The activity to request more time for.</param>
    /// <param name="minutes">Number of additional minutes requested.</param>
    /// <param name="message">Optional message to the parent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created request with ID and status secret.</returns>
    /// <exception cref="UnpairedException">If the API returns 401/403.</exception>
    /// <exception cref="ApiException">On other API failures.</exception>
    public async Task<RequestResult> RequestMoreTimeAsync(
        string accessToken,
        string userId,
        int activityId,
        int minutes,
        string message = "",
        CancellationToken cancellationToken = default)
    {
        return await CreateRequestAsync(accessToken, userId, new Dictionary<string, object>
        {
            ["type"] = RequestType.MoreTime.Value(),
            ["activityId"] = activityId,
            ["minutes"] = minutes,
            ["message"] = message,
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Request a day type change.
    /// </summary>
    /// <param name="accessToken">Valid OAuth2 access token.</param>
    /// <param name="userId">The integrating application's user ID.</param>
    /// <param name="dayTypeId">The desired day type ID.</param>
    /// <param name="message">Optional message to the parent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created request.</returns>
    /// <exception cref="UnpairedException">If the API returns 401/403.</exception>
    /// <exception cref="ApiException">On other API failures.</exception>
    public async Task<RequestResult> RequestDayTypeChangeAsync(
        string accessToken,
        string userId,
        int dayTypeId,
        string message = "",
        CancellationToken cancellationToken = default)
    {
        return await CreateRequestAsync(accessToken, userId, new Dictionary<string, object>
        {
            ["type"] = RequestType.DayTypeChange.Value(),
            ["dayTypeId"] = dayTypeId,
            ["message"] = message,
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Request lifting a ban on an activity.
    /// </summary>
    /// <param name="accessToken">Valid OAuth2 access token.</param>
    /// <param name="userId">The integrating application's user ID.</param>
    /// <param name="activityId">The banned activity to request lifting.</param>
    /// <param name="message">Optional message to the parent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created request.</returns>
    /// <exception cref="UnpairedException">If the API returns 401/403.</exception>
    /// <exception cref="ApiException">On other API failures.</exception>
    public async Task<RequestResult> RequestBanLiftAsync(
        string accessToken,
        string userId,
        int activityId,
        string message = "",
        CancellationToken cancellationToken = default)
    {
        return await CreateRequestAsync(accessToken, userId, new Dictionary<string, object>
        {
            ["type"] = RequestType.BanLift.Value(),
            ["activityId"] = activityId,
            ["message"] = message,
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Poll the status of an existing request.
    /// </summary>
    /// <param name="requestId">The request ID from createRequest.</param>
    /// <param name="statusSecret">The status secret from createRequest.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current status: "pending", "approved", or "denied".</returns>
    /// <exception cref="ApiException">On API failures.</exception>
    public async Task<string> GetRequestStatusAsync(
        string requestId,
        string statusSecret,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"{_apiHost}/request/{Uri.EscapeDataString(requestId)}/status",
            new Dictionary<string, string> { ["X-Status-Secret"] = statusSecret },
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccess)
        {
            Dictionary<string, object>? body = null;
            try { body = response.JsonAsync(); } catch { /* ignore parse errors */ }
            throw new ApiException(
                message: $"Failed to get request status: HTTP {response.StatusCode}",
                httpStatusCode: response.StatusCode,
                responseBody: body);
        }

        var data = response.JsonAsync();
        return data.TryGetValue("status", out var status) ? status.ToString()! : "pending";
    }

    /// <summary>
    /// Obtain a temporary request token from /request/tempToken.
    /// </summary>
    /// <param name="accessToken">Valid OAuth2 access token.</param>
    /// <param name="nonce">Optional nonce for idempotency.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw response dictionary with token data.</returns>
    /// <exception cref="UnpairedException">If the API returns 401/403.</exception>
    /// <exception cref="ApiException">On other API failures.</exception>
    public async Task<Dictionary<string, object>> GetTempTokenAsync(
        string accessToken,
        string? nonce = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object> { ["access_token"] = accessToken };

        if (nonce != null)
        {
            payload["nonce"] = nonce;
        }

        var response = await _httpClient.PostAsync(
            $"{_apiHost}/request/tempToken",
            payload,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.IsUnauthorized)
        {
            throw new UnpairedException("");
        }

        if (!response.IsSuccess)
        {
            Dictionary<string, object>? body = null;
            try { body = response.JsonAsync(); } catch { /* ignore parse errors */ }
            throw new ApiException(
                message: $"Failed to obtain request token: HTTP {response.StatusCode}",
                httpStatusCode: response.StatusCode,
                responseBody: body);
        }

        return response.JsonAsync();
    }

    /// <summary>
    /// Obtain a temporary token for creating a request, then create it.
    /// </summary>
    private async Task<RequestResult> CreateRequestAsync(
        string accessToken,
        string userId,
        Dictionary<string, object> payload,
        CancellationToken cancellationToken)
    {
        // Step 1: Get a temporary request token
        var tempData = await GetTempTokenAsync(accessToken, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        string? tempToken = null;
        foreach (var key in new[] { "token", "requestToken", "tempToken" })
        {
            if (tempData.TryGetValue(key, out var val))
            {
                tempToken = val.ToString();
                break;
            }
        }

        if (tempToken == null)
        {
            throw new ApiException(
                message: "Temporary request token not found in API response",
                httpStatusCode: 200,
                responseBody: tempData);
        }

        // Step 2: Create the request
        payload["requestToken"] = tempToken;
        payload["access_token"] = accessToken;

        var createResponse = await _httpClient.PostAsync(
            $"{_apiHost}/request/createRequest",
            payload,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (createResponse.IsUnauthorized)
        {
            throw new UnpairedException(userId);
        }

        if (!createResponse.IsSuccess)
        {
            Dictionary<string, object>? body = null;
            try { body = createResponse.JsonAsync(); } catch { /* ignore parse errors */ }
            throw new ApiException(
                message: $"Failed to create request: HTTP {createResponse.StatusCode}",
                httpStatusCode: createResponse.StatusCode,
                responseBody: body);
        }

        return RequestResult.FromApiResponse(createResponse.JsonAsync());
    }
}
