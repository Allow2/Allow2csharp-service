// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

namespace Allow2.Service.Models;

/// <summary>
/// Result of a permission check from the Allow2 service API.
///
/// Contains the overall allowed status, per-activity breakdown,
/// and current/upcoming day type information.
/// </summary>
public sealed class CheckResult
{
    /// <summary>Whether the child is globally allowed right now.</summary>
    public bool Allowed { get; }

    /// <summary>Per-activity permission breakdown.</summary>
    public IReadOnlyList<Activity> Activities { get; }

    /// <summary>The current day type.</summary>
    public DayType? TodayDayType { get; }

    /// <summary>The upcoming day type (if provided).</summary>
    public DayType? TomorrowDayType { get; }

    /// <summary>The raw API response for advanced use.</summary>
    public Dictionary<string, object> Raw { get; }

    public CheckResult(
        bool allowed,
        IReadOnlyList<Activity> activities,
        DayType? todayDayType,
        DayType? tomorrowDayType,
        Dictionary<string, object>? raw = null)
    {
        Allowed = allowed;
        Activities = activities;
        TodayDayType = todayDayType;
        TomorrowDayType = tomorrowDayType;
        Raw = raw ?? new Dictionary<string, object>();
    }

    /// <summary>
    /// Get a specific activity by ID, or null if not present.
    /// </summary>
    public Activity? GetActivity(int activityId)
    {
        foreach (var activity in Activities)
        {
            if (activity.Id == activityId)
            {
                return activity;
            }
        }
        return null;
    }

    /// <summary>
    /// Check whether a specific activity is allowed.
    /// </summary>
    public bool IsActivityAllowed(int activityId)
    {
        var activity = GetActivity(activityId);
        return activity != null && activity.Allowed;
    }

    /// <summary>
    /// Get remaining seconds for a specific activity.
    /// </summary>
    public int GetRemainingSeconds(int activityId)
    {
        var activity = GetActivity(activityId);
        return activity?.Remaining ?? 0;
    }

    /// <summary>
    /// Build from the raw API response.
    /// </summary>
    /// <param name="response">The decoded JSON response from /serviceapi/check.</param>
    public static CheckResult FromApiResponse(Dictionary<string, object> response)
    {
        var activities = new List<Activity>();
        if (response.TryGetValue("activities", out var rawActivities) && rawActivities is List<object> actList)
        {
            foreach (var item in actList)
            {
                if (item is Dictionary<string, object> actData)
                {
                    activities.Add(Activity.FromArray(actData));
                }
            }
        }

        DayType? todayDayType = null;
        if (response.TryGetValue("dayType", out var dt) && dt is Dictionary<string, object> dtDict)
        {
            todayDayType = DayType.FromArray(dtDict);
        }
        else if (response.TryGetValue("today", out var today) && today is Dictionary<string, object> todayDict)
        {
            todayDayType = DayType.FromArray(todayDict);
        }

        DayType? tomorrowDayType = null;
        if (response.TryGetValue("tomorrowDayType", out var tdt) && tdt is Dictionary<string, object> tdtDict)
        {
            tomorrowDayType = DayType.FromArray(tdtDict);
        }
        else if (response.TryGetValue("tomorrow", out var tomorrow) && tomorrow is Dictionary<string, object> tomorrowDict)
        {
            tomorrowDayType = DayType.FromArray(tomorrowDict);
        }

        // Global "allowed" is true only if ALL activities are allowed
        var allowed = true;
        foreach (var act in activities)
        {
            if (!act.Allowed)
            {
                allowed = false;
                break;
            }
        }

        // Override with explicit server value if present
        if (response.TryGetValue("allowed", out var allowedVal))
        {
            allowed = Convert.ToBoolean(allowedVal);
        }

        return new CheckResult(
            allowed: allowed,
            activities: activities,
            todayDayType: todayDayType,
            tomorrowDayType: tomorrowDayType,
            raw: response);
    }
}
