// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

namespace Allow2.Service.Models;

/// <summary>
/// Represents the permission state of a single activity from a check result.
/// </summary>
public sealed class Activity
{
    /// <summary>Screen Time activity ID -- master device-level switch.</summary>
    public const int ScreenTime = 8;

    /// <summary>Gaming activity ID.</summary>
    public const int Gaming = 3;

    /// <summary>Internet activity ID.</summary>
    public const int Internet = 1;

    /// <summary>Social Media activity ID.</summary>
    public const int Social = 6;

    /// <summary>The activity ID.</summary>
    public int Id { get; }

    /// <summary>The activity name.</summary>
    public string Name { get; }

    /// <summary>Whether this activity is currently allowed.</summary>
    public bool Allowed { get; }

    /// <summary>Remaining seconds for this activity.</summary>
    public int Remaining { get; }

    /// <summary>Whether this activity is banned (indefinite restriction).</summary>
    public bool Banned { get; }

    /// <summary>Whether the current time block allows this activity.</summary>
    public bool TimeBlockAllowed { get; }

    public Activity(int id, string name, bool allowed, int remaining, bool banned, bool timeBlockAllowed)
    {
        Id = id;
        Name = name;
        Allowed = allowed;
        Remaining = remaining;
        Banned = banned;
        TimeBlockAllowed = timeBlockAllowed;
    }

    /// <summary>
    /// Create from a single activity entry in the API check response.
    /// </summary>
    public static Activity FromArray(Dictionary<string, object> data)
    {
        return new Activity(
            id: Convert.ToInt32(data.GetValueOrDefault("id", 0)),
            name: (data.GetValueOrDefault("activity") ?? data.GetValueOrDefault("name") ?? "").ToString()!,
            allowed: Convert.ToBoolean(data.GetValueOrDefault("allowed", false)),
            remaining: Convert.ToInt32(data.GetValueOrDefault("remaining", 0)),
            banned: Convert.ToBoolean(data.GetValueOrDefault("banned", false)),
            timeBlockAllowed: Convert.ToBoolean(
                data.GetValueOrDefault("timeblock") ??
                data.GetValueOrDefault("timeBlockAllowed") ??
                true));
    }
}
