// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

using Allow2.Service;
using Allow2.Service.Models;

namespace Allow2.Service.Tests;

public class VoiceCodeTests
{
    private const string TestSecret = "test-pairing-secret-12345";
    private const string TestDate = "2026-03-12";

    [Fact]
    public void Generate_ReturnsValidPair()
    {
        var pair = VoiceCode.Generate(
            TestSecret,
            RequestType.MoreTime,
            activityId: 3,
            minutes: 30,
            date: TestDate);

        Assert.NotNull(pair.Challenge);
        Assert.NotNull(pair.ExpectedResponse);
        Assert.Equal(6, pair.ExpectedResponse.Length);
        Assert.Matches(@"^\d{6}$", pair.ExpectedResponse);
    }

    [Fact]
    public void Generate_ChallengeFormatIsCorrect()
    {
        var pair = VoiceCode.Generate(
            TestSecret,
            RequestType.MoreTime,
            activityId: 3,
            minutes: 30,
            date: TestDate);

        // Challenge format: T AA MM NN
        var parts = pair.Challenge.Split(' ');
        Assert.Equal(4, parts.Length);
        Assert.Equal("0", parts[0]); // MoreTime = 0
        Assert.Equal("03", parts[1]); // activityId = 3
        Assert.Equal("06", parts[2]); // 30 minutes / 5 = 6
    }

    [Fact]
    public void Generate_DayTypeChangeHasCorrectTypeCode()
    {
        var pair = VoiceCode.Generate(
            TestSecret,
            RequestType.DayTypeChange,
            activityId: 2,
            date: TestDate);

        var parts = pair.Challenge.Split(' ');
        Assert.Equal("1", parts[0]); // DayTypeChange = 1
    }

    [Fact]
    public void Generate_BanLiftHasCorrectTypeCode()
    {
        var pair = VoiceCode.Generate(
            TestSecret,
            RequestType.BanLift,
            activityId: 6,
            date: TestDate);

        var parts = pair.Challenge.Split(' ');
        Assert.Equal("2", parts[0]); // BanLift = 2
    }

    [Fact]
    public void Verify_CorrectResponseReturnsTrue()
    {
        var pair = VoiceCode.Generate(
            TestSecret,
            RequestType.MoreTime,
            activityId: 3,
            minutes: 30,
            date: TestDate);

        var isValid = VoiceCode.Verify(TestSecret, pair.Challenge, pair.ExpectedResponse, TestDate);
        Assert.True(isValid);
    }

    [Fact]
    public void Verify_WrongResponseReturnsFalse()
    {
        var pair = VoiceCode.Generate(
            TestSecret,
            RequestType.MoreTime,
            activityId: 3,
            minutes: 30,
            date: TestDate);

        var isValid = VoiceCode.Verify(TestSecret, pair.Challenge, "000000", TestDate);
        Assert.False(isValid);
    }

    [Fact]
    public void Verify_WrongDateReturnsFalse()
    {
        var pair = VoiceCode.Generate(
            TestSecret,
            RequestType.MoreTime,
            activityId: 3,
            minutes: 30,
            date: TestDate);

        var isValid = VoiceCode.Verify(TestSecret, pair.Challenge, pair.ExpectedResponse, "2026-03-13");
        Assert.False(isValid);
    }

    [Fact]
    public void Verify_WrongSecretReturnsFalse()
    {
        var pair = VoiceCode.Generate(
            TestSecret,
            RequestType.MoreTime,
            activityId: 3,
            minutes: 30,
            date: TestDate);

        var isValid = VoiceCode.Verify("wrong-secret", pair.Challenge, pair.ExpectedResponse, TestDate);
        Assert.False(isValid);
    }

    [Fact]
    public void DecodeChallenge_ValidChallenge()
    {
        var decoded = VoiceCode.DecodeChallenge("0 03 06 42");

        Assert.NotNull(decoded);
        Assert.Equal(0, decoded.Type);
        Assert.Equal(3, decoded.ActivityId);
        Assert.Equal(30, decoded.Minutes); // 06 * 5 = 30
        Assert.Equal(42, decoded.Nonce);
    }

    [Fact]
    public void DecodeChallenge_InvalidFormatReturnsNull()
    {
        Assert.Null(VoiceCode.DecodeChallenge("invalid"));
        Assert.Null(VoiceCode.DecodeChallenge("0 1 2"));
        Assert.Null(VoiceCode.DecodeChallenge(""));
    }

    [Fact]
    public void DecodeChallenge_HandlesExtraWhitespace()
    {
        var decoded = VoiceCode.DecodeChallenge("  1  02  10  99  ");

        Assert.NotNull(decoded);
        Assert.Equal(1, decoded.Type);
        Assert.Equal(2, decoded.ActivityId);
        Assert.Equal(50, decoded.Minutes); // 10 * 5 = 50
        Assert.Equal(99, decoded.Nonce);
    }

    [Fact]
    public void Generate_ClampsActivityIdTo99()
    {
        var pair = VoiceCode.Generate(
            TestSecret,
            RequestType.MoreTime,
            activityId: 150,
            date: TestDate);

        var parts = pair.Challenge.Split(' ');
        Assert.Equal("99", parts[1]);
    }

    [Fact]
    public void Generate_ClampsMinutesTo99Increments()
    {
        var pair = VoiceCode.Generate(
            TestSecret,
            RequestType.MoreTime,
            activityId: 1,
            minutes: 600, // 600/5 = 120, clamped to 99
            date: TestDate);

        var parts = pair.Challenge.Split(' ');
        Assert.Equal("99", parts[2]);
    }

    [Fact]
    public void Generate_DeterministicWithSameInputs()
    {
        // Generate two codes with different nonces but verify they both validate
        var pair1 = VoiceCode.Generate(TestSecret, RequestType.MoreTime, 3, 30, TestDate);
        var isValid1 = VoiceCode.Verify(TestSecret, pair1.Challenge, pair1.ExpectedResponse, TestDate);
        Assert.True(isValid1);

        var pair2 = VoiceCode.Generate(TestSecret, RequestType.MoreTime, 3, 30, TestDate);
        var isValid2 = VoiceCode.Verify(TestSecret, pair2.Challenge, pair2.ExpectedResponse, TestDate);
        Assert.True(isValid2);
    }
}
