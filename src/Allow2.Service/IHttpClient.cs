// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

namespace Allow2.Service;

/// <summary>
/// HTTP client interface for Allow2 API calls.
/// Implementations must handle JSON encoding/decoding and return
/// an <see cref="HttpResponse"/> value object.
/// </summary>
public interface IHttpClient
{
    /// <summary>
    /// Send a POST request.
    /// </summary>
    /// <param name="url">Full URL to send to.</param>
    /// <param name="data">Body data (will be JSON-encoded by the implementation).</param>
    /// <param name="headers">Additional headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The HTTP response.</returns>
    Task<HttpResponse> PostAsync(
        string url,
        Dictionary<string, object>? data = null,
        Dictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a GET request.
    /// </summary>
    /// <param name="url">Full URL to send to.</param>
    /// <param name="headers">Additional headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The HTTP response.</returns>
    Task<HttpResponse> GetAsync(
        string url,
        Dictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default);
}
