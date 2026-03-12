// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

namespace Allow2.Service.Models;

/// <summary>
/// Categories for feedback submissions.
/// </summary>
public enum FeedbackCategory
{
    /// <summary>Bug report.</summary>
    Bug,

    /// <summary>Feature request.</summary>
    FeatureRequest,

    /// <summary>Something is not working as expected.</summary>
    NotWorking,

    /// <summary>Other feedback.</summary>
    Other,
}

/// <summary>
/// Extension methods for <see cref="FeedbackCategory"/>.
/// </summary>
public static class FeedbackCategoryExtensions
{
    /// <summary>
    /// Get the API string value for this feedback category.
    /// </summary>
    public static string Value(this FeedbackCategory category)
    {
        return category switch
        {
            FeedbackCategory.Bug => "bug",
            FeedbackCategory.FeatureRequest => "feature_request",
            FeedbackCategory.NotWorking => "not_working",
            FeedbackCategory.Other => "other",
            _ => throw new ArgumentOutOfRangeException(nameof(category)),
        };
    }
}
