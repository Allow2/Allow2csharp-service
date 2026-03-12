// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

namespace Allow2.Service.Models;

/// <summary>
/// A challenge-response pair for offline voice code approval.
///
/// The challenge is displayed to the child, who reads it to the parent.
/// The parent's Allow2 app computes the response; the child enters it.
/// </summary>
/// <param name="Challenge">The challenge code to display to the child.</param>
/// <param name="ExpectedResponse">The expected 6-digit response code.</param>
public record VoiceCodePair(string Challenge, string ExpectedResponse);
