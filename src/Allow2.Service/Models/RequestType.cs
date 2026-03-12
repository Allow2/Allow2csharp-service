// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

namespace Allow2.Service.Models;

/// <summary>
/// Types of requests a child can make.
/// </summary>
public enum RequestType
{
    /// <summary>Request additional time for an activity.</summary>
    MoreTime,

    /// <summary>Request a day type change (e.g., treat today as a weekend).</summary>
    DayTypeChange,

    /// <summary>Request lifting a ban on an activity.</summary>
    BanLift,
}

/// <summary>
/// Extension methods for <see cref="RequestType"/>.
/// </summary>
public static class RequestTypeExtensions
{
    /// <summary>
    /// Get the API string value for this request type.
    /// </summary>
    public static string Value(this RequestType type)
    {
        return type switch
        {
            RequestType.MoreTime => "extension",
            RequestType.DayTypeChange => "daytype",
            RequestType.BanLift => "banlift",
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
    }

    /// <summary>
    /// Numeric code used in voice challenge generation.
    /// T field in the T A MM NN format.
    /// </summary>
    public static int VoiceCodeValue(this RequestType type)
    {
        return type switch
        {
            RequestType.MoreTime => 0,
            RequestType.DayTypeChange => 1,
            RequestType.BanLift => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
    }
}
