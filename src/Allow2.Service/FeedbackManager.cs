// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

using Allow2.Service.Exceptions;
using Allow2.Service.Models;

namespace Allow2.Service;

/// <summary>
/// Manages feedback submission and discussion threads via the Allow2 API.
///
/// Feedback allows users to report issues, request features, or communicate
/// with the Allow2 Parental Freedom support team.
/// </summary>
internal sealed class FeedbackManager
{
    private readonly IHttpClient _httpClient;
    private readonly string _apiHost;

    public FeedbackManager(IHttpClient httpClient, string apiHost)
    {
        _httpClient = httpClient;
        _apiHost = apiHost;
    }

    /// <summary>
    /// Submit new feedback.
    /// </summary>
    /// <param name="accessToken">Valid OAuth2 access token.</param>
    /// <param name="userId">The integrating application's user ID.</param>
    /// <param name="category">The feedback category.</param>
    /// <param name="message">The feedback message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The discussion ID for the created feedback thread.</returns>
    /// <exception cref="UnpairedException">If the API returns 401/403.</exception>
    /// <exception cref="ApiException">On other API failures.</exception>
    public async Task<string> SubmitAsync(
        string accessToken,
        string userId,
        FeedbackCategory category,
        string message,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync(
            $"{_apiHost}/feedback/submit",
            new Dictionary<string, object>
            {
                ["access_token"] = accessToken,
                ["category"] = category.Value(),
                ["message"] = message,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.IsUnauthorized)
        {
            throw new UnpairedException(userId);
        }

        if (!response.IsSuccess)
        {
            Dictionary<string, object>? body = null;
            try { body = response.JsonAsync(); } catch { /* ignore parse errors */ }
            throw new ApiException(
                message: $"Failed to submit feedback: HTTP {response.StatusCode}",
                httpStatusCode: response.StatusCode,
                responseBody: body);
        }

        var data = response.JsonAsync();
        foreach (var key in new[] { "discussionId", "feedbackId", "id" })
        {
            if (data.TryGetValue(key, out var val))
            {
                return val.ToString()!;
            }
        }
        return "";
    }

    /// <summary>
    /// Load all feedback discussions for the authenticated user.
    /// </summary>
    /// <param name="accessToken">Valid OAuth2 access token.</param>
    /// <param name="userId">The integrating application's user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of feedback discussion threads.</returns>
    /// <exception cref="UnpairedException">If the API returns 401/403.</exception>
    /// <exception cref="ApiException">On other API failures.</exception>
    public async Task<List<object>> LoadAsync(
        string accessToken,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"{_apiHost}/feedback/load",
            new Dictionary<string, string> { ["Authorization"] = $"Bearer {accessToken}" },
            cancellationToken).ConfigureAwait(false);

        if (response.IsUnauthorized)
        {
            throw new UnpairedException(userId);
        }

        if (!response.IsSuccess)
        {
            Dictionary<string, object>? body = null;
            try { body = response.JsonAsync(); } catch { /* ignore parse errors */ }
            throw new ApiException(
                message: $"Failed to load feedback: HTTP {response.StatusCode}",
                httpStatusCode: response.StatusCode,
                responseBody: body);
        }

        var data = response.JsonAsync();
        foreach (var key in new[] { "discussions", "feedback" })
        {
            if (data.TryGetValue(key, out var val) && val is List<object> list)
            {
                return list;
            }
        }
        return new List<object>();
    }

    /// <summary>
    /// Reply to an existing feedback discussion thread.
    /// </summary>
    /// <param name="accessToken">Valid OAuth2 access token.</param>
    /// <param name="userId">The integrating application's user ID.</param>
    /// <param name="discussionId">The discussion thread ID to reply to.</param>
    /// <param name="message">The reply message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="UnpairedException">If the API returns 401/403.</exception>
    /// <exception cref="ApiException">On other API failures.</exception>
    public async Task ReplyAsync(
        string accessToken,
        string userId,
        string discussionId,
        string message,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync(
            $"{_apiHost}/feedback/{Uri.EscapeDataString(discussionId)}/reply",
            new Dictionary<string, object>
            {
                ["access_token"] = accessToken,
                ["message"] = message,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.IsUnauthorized)
        {
            throw new UnpairedException(userId);
        }

        if (!response.IsSuccess)
        {
            Dictionary<string, object>? body = null;
            try { body = response.JsonAsync(); } catch { /* ignore parse errors */ }
            throw new ApiException(
                message: $"Failed to reply to feedback: HTTP {response.StatusCode}",
                httpStatusCode: response.StatusCode,
                responseBody: body);
        }
    }
}
