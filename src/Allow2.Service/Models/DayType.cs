// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

namespace Allow2.Service.Models;

/// <summary>
/// Represents a day type in the Allow2 system (e.g., School Day, Weekend, Holiday).
/// </summary>
public sealed class DayType
{
    /// <summary>The day type ID.</summary>
    public int Id { get; }

    /// <summary>The day type name.</summary>
    public string Name { get; }

    public DayType(int id, string name)
    {
        Id = id;
        Name = name;
    }

    /// <summary>
    /// Create from API response data.
    /// </summary>
    public static DayType FromArray(Dictionary<string, object> data)
    {
        return new DayType(
            id: Convert.ToInt32(data["id"]),
            name: data["name"].ToString()!);
    }
}
