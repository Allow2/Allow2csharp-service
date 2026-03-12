// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Allow2.Service.Exceptions;

namespace Allow2.Service;

/// <summary>
/// Default HTTP client implementation using <see cref="System.Net.Http.HttpClient"/>.
/// No external dependencies -- uses the built-in .NET HTTP stack.
/// </summary>
public sealed class Allow2HttpClient : IHttpClient
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    private readonly HttpClient _httpClient;

    /// <summary>
    /// Create a new Allow2HttpClient with an optional pre-configured HttpClient.
    /// </summary>
    /// <param name="httpClient">Custom HttpClient, or null to create a default one.</param>
    public Allow2HttpClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = DefaultTimeout };

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Allow2-CSharp-Service-SDK/2.0");
        }
    }

    /// <inheritdoc />
    public async Task<HttpResponse> PostAsync(
        string url,
        Dictionary<string, object>? data = null,
        Dictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return await RequestAsync(HttpMethod.Post, url, data, headers, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<HttpResponse> GetAsync(
        string url,
        Dictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return await RequestAsync(HttpMethod.Get, url, null, headers, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponse> RequestAsync(
        HttpMethod method,
        string url,
        Dictionary<string, object>? data,
        Dictionary<string, string>? headers,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (headers != null)
        {
            foreach (var (name, value) in headers)
            {
                request.Headers.TryAddWithoutValidation(name, value);
            }
        }

        if (method == HttpMethod.Post && data != null)
        {
            var json = JsonSerializer.Serialize(data);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new ApiException(
                message: $"HTTP request failed: {ex.Message}",
                httpStatusCode: 0,
                responseBody: null);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        return new HttpResponse(
            statusCode: (int)response.StatusCode,
            body: body);
    }
}
