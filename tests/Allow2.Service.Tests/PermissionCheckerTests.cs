// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

using Allow2.Service;

namespace Allow2.Service.Tests;

public class PermissionCheckerTests
{
    [Fact]
    public void NormalizeActivities_SimpleIntList()
    {
        var activities = new List<object> { 1, 3, 8 };

        var normalized = PermissionChecker.NormalizeActivities(activities);

        Assert.Equal(3, normalized.Count);

        Assert.Equal(1, normalized[0]["id"]);
        Assert.Equal(true, normalized[0]["log"]);

        Assert.Equal(3, normalized[1]["id"]);
        Assert.Equal(true, normalized[1]["log"]);

        Assert.Equal(8, normalized[2]["id"]);
        Assert.Equal(true, normalized[2]["log"]);
    }

    [Fact]
    public void NormalizeActivities_DictionaryFormat()
    {
        var activities = new List<object>
        {
            new Dictionary<string, object> { ["id"] = 1, ["log"] = true },
            new Dictionary<string, object> { ["id"] = 3, ["log"] = false },
        };

        var normalized = PermissionChecker.NormalizeActivities(activities);

        Assert.Equal(2, normalized.Count);
        Assert.Equal(1, normalized[0]["id"]);
        Assert.Equal(true, normalized[0]["log"]);
        Assert.Equal(3, normalized[1]["id"]);
        Assert.Equal(false, normalized[1]["log"]);
    }

    [Fact]
    public void NormalizeActivities_EmptyList()
    {
        var activities = new List<object>();

        var normalized = PermissionChecker.NormalizeActivities(activities);

        Assert.Empty(normalized);
    }

    [Fact]
    public void NormalizeActivities_LongValues()
    {
        // JSON deserialization often produces long (Int64) values
        var activities = new List<object> { 1L, 3L, 8L };

        var normalized = PermissionChecker.NormalizeActivities(activities);

        Assert.Equal(3, normalized.Count);
        Assert.Equal(1, normalized[0]["id"]);
        Assert.Equal(3, normalized[1]["id"]);
        Assert.Equal(8, normalized[2]["id"]);
    }
}
