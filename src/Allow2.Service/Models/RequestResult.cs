// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

namespace Allow2.Service.Models;

/// <summary>
/// Result of creating a request (more time, day type change, or ban lift).
/// </summary>
public sealed class RequestResult
{
    /// <summary>The request ID for polling status.</summary>
    public string RequestId { get; }

    /// <summary>The status secret for authenticating status polls.</summary>
    public string StatusSecret { get; }

    /// <summary>The current status of the request.</summary>
    public string Status { get; }

    public RequestResult(string requestId, string statusSecret, string status)
    {
        RequestId = requestId;
        StatusSecret = statusSecret;
        Status = status;
    }

    /// <summary>
    /// Create from API response.
    /// </summary>
    public static RequestResult FromApiResponse(Dictionary<string, object> response)
    {
        return new RequestResult(
            requestId: response["requestId"].ToString()!,
            statusSecret: response["statusSecret"].ToString()!,
            status: (response.GetValueOrDefault("status") ?? "pending").ToString()!);
    }

    /// <summary>Whether the request is still pending.</summary>
    public bool IsPending => Status == "pending";

    /// <summary>Whether the request has been approved.</summary>
    public bool IsApproved => Status == "approved";

    /// <summary>Whether the request has been denied.</summary>
    public bool IsDenied => Status == "denied";
}
