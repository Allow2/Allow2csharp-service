// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

using Allow2.Service.Models;

namespace Allow2.Service.Tests;

public class CheckResultTests
{
    [Fact]
    public void FromApiResponse_ParsesActivities()
    {
        var response = new Dictionary<string, object>
        {
            ["activities"] = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["id"] = 1L,
                    ["activity"] = "Internet",
                    ["allowed"] = true,
                    ["remaining"] = 3600L,
                    ["banned"] = false,
                    ["timeblock"] = true,
                },
                new Dictionary<string, object>
                {
                    ["id"] = 3L,
                    ["activity"] = "Gaming",
                    ["allowed"] = false,
                    ["remaining"] = 0L,
                    ["banned"] = true,
                    ["timeblock"] = true,
                },
            },
        };

        var result = CheckResult.FromApiResponse(response);

        Assert.Equal(2, result.Activities.Count);
        Assert.False(result.Allowed); // Gaming is not allowed

        var internet = result.GetActivity(1);
        Assert.NotNull(internet);
        Assert.Equal("Internet", internet!.Name);
        Assert.True(internet.Allowed);
        Assert.Equal(3600, internet.Remaining);
        Assert.False(internet.Banned);

        var gaming = result.GetActivity(3);
        Assert.NotNull(gaming);
        Assert.Equal("Gaming", gaming!.Name);
        Assert.False(gaming.Allowed);
        Assert.True(gaming.Banned);
    }

    [Fact]
    public void FromApiResponse_ParsesDayType()
    {
        var response = new Dictionary<string, object>
        {
            ["activities"] = new List<object>(),
            ["dayType"] = new Dictionary<string, object>
            {
                ["id"] = 1L,
                ["name"] = "School Day",
            },
            ["tomorrowDayType"] = new Dictionary<string, object>
            {
                ["id"] = 2L,
                ["name"] = "Weekend",
            },
        };

        var result = CheckResult.FromApiResponse(response);

        Assert.NotNull(result.TodayDayType);
        Assert.Equal(1, result.TodayDayType!.Id);
        Assert.Equal("School Day", result.TodayDayType.Name);

        Assert.NotNull(result.TomorrowDayType);
        Assert.Equal(2, result.TomorrowDayType!.Id);
        Assert.Equal("Weekend", result.TomorrowDayType.Name);
    }

    [Fact]
    public void FromApiResponse_ParsesAlternativeDayTypeKeys()
    {
        var response = new Dictionary<string, object>
        {
            ["activities"] = new List<object>(),
            ["today"] = new Dictionary<string, object>
            {
                ["id"] = 3L,
                ["name"] = "Holiday",
            },
            ["tomorrow"] = new Dictionary<string, object>
            {
                ["id"] = 1L,
                ["name"] = "School Day",
            },
        };

        var result = CheckResult.FromApiResponse(response);

        Assert.NotNull(result.TodayDayType);
        Assert.Equal("Holiday", result.TodayDayType!.Name);
        Assert.NotNull(result.TomorrowDayType);
        Assert.Equal("School Day", result.TomorrowDayType!.Name);
    }

    [Fact]
    public void FromApiResponse_ExplicitAllowedOverridesCalculated()
    {
        var response = new Dictionary<string, object>
        {
            ["allowed"] = true,
            ["activities"] = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["id"] = 1L,
                    ["allowed"] = false, // Activity says not allowed
                },
            },
        };

        var result = CheckResult.FromApiResponse(response);
        Assert.True(result.Allowed); // Explicit server value overrides
    }

    [Fact]
    public void IsActivityAllowed_ReturnsFalseForMissingActivity()
    {
        var response = new Dictionary<string, object>
        {
            ["activities"] = new List<object>(),
        };

        var result = CheckResult.FromApiResponse(response);
        Assert.False(result.IsActivityAllowed(99));
    }

    [Fact]
    public void GetRemainingSeconds_ReturnsZeroForMissingActivity()
    {
        var response = new Dictionary<string, object>
        {
            ["activities"] = new List<object>(),
        };

        var result = CheckResult.FromApiResponse(response);
        Assert.Equal(0, result.GetRemainingSeconds(99));
    }

    [Fact]
    public void FromApiResponse_HandlesEmptyResponse()
    {
        var response = new Dictionary<string, object>();

        var result = CheckResult.FromApiResponse(response);

        Assert.True(result.Allowed); // No activities = all allowed
        Assert.Empty(result.Activities);
        Assert.Null(result.TodayDayType);
        Assert.Null(result.TomorrowDayType);
    }

    [Fact]
    public void FromApiResponse_ActivityNameFallback()
    {
        var response = new Dictionary<string, object>
        {
            ["activities"] = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["id"] = 1L,
                    ["name"] = "Internet Access",
                    ["allowed"] = true,
                },
            },
        };

        var result = CheckResult.FromApiResponse(response);
        Assert.Equal("Internet Access", result.Activities[0].Name);
    }
}
