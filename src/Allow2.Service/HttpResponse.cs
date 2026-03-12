// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

using System.Text.Json;

namespace Allow2.Service;

/// <summary>
/// Simple value object representing an HTTP response.
/// </summary>
public sealed class HttpResponse
{
    private Dictionary<string, object>? _decoded;

    /// <summary>
    /// The HTTP status code.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// The raw response body.
    /// </summary>
    public string Body { get; }

    public HttpResponse(int statusCode, string body)
    {
        StatusCode = statusCode;
        Body = body;
    }

    /// <summary>
    /// Decode the response body as JSON.
    /// </summary>
    /// <returns>The decoded JSON as a dictionary.</returns>
    /// <exception cref="JsonException">If the body is not valid JSON.</exception>
    public Dictionary<string, object> JsonAsync()
    {
        if (_decoded == null)
        {
            var element = JsonSerializer.Deserialize<JsonElement>(Body);
            _decoded = JsonElementToDictionary(element);
        }
        return _decoded;
    }

    /// <summary>
    /// Whether the HTTP status code indicates success (2xx).
    /// </summary>
    public bool IsSuccess => StatusCode >= 200 && StatusCode < 300;

    /// <summary>
    /// Whether the HTTP status code indicates an authentication/authorization error.
    /// </summary>
    public bool IsUnauthorized => StatusCode == 401 || StatusCode == 403;

    /// <summary>
    /// Convert a JsonElement to a dictionary recursively.
    /// </summary>
    internal static Dictionary<string, object> JsonElementToDictionary(JsonElement element)
    {
        var dict = new Dictionary<string, object>();

        if (element.ValueKind != JsonValueKind.Object)
        {
            return dict;
        }

        foreach (var property in element.EnumerateObject())
        {
            dict[property.Name] = ConvertJsonElement(property.Value);
        }

        return dict;
    }

    internal static object ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => JsonElementToDictionary(element),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
            JsonValueKind.String => element.GetString()!,
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            _ => element.GetRawText(),
        };
    }
}
