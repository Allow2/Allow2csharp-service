// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

using System.Security.Cryptography;
using System.Text;
using Allow2.Service.Models;

namespace Allow2.Service;

/// <summary>
/// HMAC-SHA256 challenge-response system for offline voice code approval.
///
/// When a child is offline (or the parent's approval channel is offline), the child
/// can read a challenge code to the parent (over phone/voice). The parent's Allow2 app
/// computes the matching response code. The child enters it to get temporary approval.
///
/// Challenge format: T AA MM NN
///   T  = Request type (0 = more time, 1 = day type change, 2 = ban lift)
///   AA = Activity ID (00-99)
///   MM = Minutes in 5-minute increments (00-99, so 0-495 minutes)
///   NN = Random nonce (00-99)
///
/// Response: First 6 digits of HMAC-SHA256(secret, challenge + date)
/// Date-bound: expires at midnight in the child's timezone.
/// </summary>
public static class VoiceCode
{
    /// <summary>
    /// Generate a challenge-response pair for offline approval.
    /// </summary>
    /// <param name="secret">The child's pairing secret (shared between parent and child device).</param>
    /// <param name="type">The type of request.</param>
    /// <param name="activityId">The activity ID (0-99).</param>
    /// <param name="minutes">Minutes requested (rounded to nearest 5-min increment, max 495).</param>
    /// <param name="date">Date string in yyyy-MM-dd format (default: today). Used for date-binding.</param>
    /// <returns>The challenge to display and the expected response.</returns>
    public static VoiceCodePair Generate(
        string secret,
        RequestType type,
        int activityId,
        int minutes = 0,
        string? date = null)
    {
        date ??= DateTime.UtcNow.ToString("yyyy-MM-dd");

        // T: request type code (0-2)
        var t = type.VoiceCodeValue();

        // A: activity ID clamped to 0-99
        var a = Math.Max(0, Math.Min(99, activityId));

        // MM: minutes in 5-minute increments, clamped to 0-99
        var mm = Math.Max(0, Math.Min(99, (int)Math.Round((double)minutes / 5)));

        // NN: random nonce 0-99
        var nn = RandomNumberGenerator.GetInt32(0, 100);

        var challenge = $"{t} {a:D2} {mm:D2} {nn:D2}";
        var expectedResponse = ComputeResponse(secret, challenge, date);

        return new VoiceCodePair(challenge, expectedResponse);
    }

    /// <summary>
    /// Verify a voice code response against a challenge.
    /// </summary>
    /// <param name="secret">The child's pairing secret.</param>
    /// <param name="challenge">The challenge code that was displayed.</param>
    /// <param name="response">The response code entered by the child.</param>
    /// <param name="date">Date string (default: today). Must match when the challenge was generated.</param>
    /// <returns>True if the response is valid.</returns>
    public static bool Verify(
        string secret,
        string challenge,
        string response,
        string? date = null)
    {
        date ??= DateTime.UtcNow.ToString("yyyy-MM-dd");
        var expected = ComputeResponse(secret, challenge, date);

        // Constant-time comparison
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var responseBytes = Encoding.UTF8.GetBytes(response);

        return CryptographicOperations.FixedTimeEquals(expectedBytes, responseBytes);
    }

    /// <summary>
    /// Decode a challenge string into its component parts.
    /// </summary>
    /// <param name="challenge">The challenge in "T AA MM NN" format.</param>
    /// <returns>Decoded parts, or null if invalid format.</returns>
    public static DecodedChallenge? DecodeChallenge(string challenge)
    {
        // Normalize whitespace
        var parts = challenge.Trim().Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 4)
        {
            return null;
        }

        if (!int.TryParse(parts[0], out var type) ||
            !int.TryParse(parts[1], out var activityId) ||
            !int.TryParse(parts[2], out var minuteIncrements) ||
            !int.TryParse(parts[3], out var nonce))
        {
            return null;
        }

        return new DecodedChallenge(type, activityId, minuteIncrements * 5, nonce);
    }

    /// <summary>
    /// Compute the 6-digit response for a challenge + date.
    /// </summary>
    /// <param name="secret">Shared secret.</param>
    /// <param name="challenge">Challenge string.</param>
    /// <param name="date">Date in yyyy-MM-dd format.</param>
    /// <returns>6-digit numeric response string.</returns>
    private static string ComputeResponse(string secret, string challenge, string date)
    {
        var message = challenge + date;
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var messageBytes = Encoding.UTF8.GetBytes(message);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(messageBytes);

        // Take first 8 hex chars (32 bits), convert to long, modulo 1,000,000 for 6 digits
        var hexString = Convert.ToHexString(hash).Substring(0, 8).ToLowerInvariant();
        var truncated = Convert.ToInt64(hexString, 16);
        var code = truncated % 1_000_000;

        return code.ToString("D6");
    }
}

/// <summary>
/// Decoded components of a voice code challenge.
/// </summary>
/// <param name="Type">Request type (0 = more time, 1 = day type change, 2 = ban lift).</param>
/// <param name="ActivityId">The activity ID.</param>
/// <param name="Minutes">Minutes requested (already multiplied by 5).</param>
/// <param name="Nonce">Random nonce.</param>
public record DecodedChallenge(int Type, int ActivityId, int Minutes, int Nonce);
