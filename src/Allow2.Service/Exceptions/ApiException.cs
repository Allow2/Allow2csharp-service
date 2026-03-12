// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

namespace Allow2.Service.Exceptions;

/// <summary>
/// Thrown when an Allow2 API call fails with an unexpected error.
/// </summary>
public class ApiException : Allow2Exception
{
    /// <summary>The HTTP status code returned by the API.</summary>
    public int HttpStatusCode { get; }

    /// <summary>The decoded response body, if available.</summary>
    public object? ResponseBody { get; }

    public ApiException(
        string message,
        int httpStatusCode,
        object? responseBody = null,
        Exception? innerException = null)
        : base(
            message,
            innerException,
            new Dictionary<string, object?>
            {
                ["httpStatusCode"] = httpStatusCode,
                ["responseBody"] = responseBody,
            })
    {
        HttpStatusCode = httpStatusCode;
        ResponseBody = responseBody;
    }
}
