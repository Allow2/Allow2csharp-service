// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

namespace Allow2.Service.Exceptions;

/// <summary>
/// Base exception for all Allow2 SDK errors.
/// </summary>
public class Allow2Exception : Exception
{
    /// <summary>
    /// Additional context about the error.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Context { get; }

    public Allow2Exception(
        string message = "",
        Exception? innerException = null,
        Dictionary<string, object?>? context = null)
        : base(message, innerException)
    {
        Context = context ?? new Dictionary<string, object?>();
    }
}
