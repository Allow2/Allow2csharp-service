# Allow2 C#/.NET Service SDK v2

[![NuGet version](https://img.shields.io/nuget/v/Allow2.Service.svg?style=flat-square)](https://www.nuget.org/packages/Allow2.Service)
[![.NET versions](https://img.shields.io/badge/.NET-6.0%20%7C%207.0%20%7C%208.0-blue?style=flat-square)](https://www.nuget.org/packages/Allow2.Service)
[![CI](https://img.shields.io/github/actions/workflow/status/Allow2/Allow2CSharp-Service/ci.yml?style=flat-square)](https://github.com/Allow2/Allow2CSharp-Service/actions)

Official Allow2 Parental Freedom **Service SDK** for .NET -- for web services with user accounts (ASP.NET, Blazor, etc.).

This is a **Service SDK** -- it runs on your web server, not on a child's device. Following industry standard practice (Stripe, Firebase, Auth0), Allow2 maintains separate Device and Service SDKs. It handles OAuth2 pairing, permission checking, all 3 request types, voice codes, and feedback via the Allow2 Service API.

| | |
|---|---|
| **Package** | `Allow2.Service` |
| **Targets** | .NET 6.0+ |
| **Dependencies** | None (uses System.Net.Http, System.Text.Json, System.Security.Cryptography) |
| **Language** | C# |

## Requirements

- .NET 6.0 or later
- No external NuGet dependencies

## Installation

```bash
dotnet add package Allow2.Service
```

## Quick Start

1. Register your application at [developer.allow2.com](https://developer.allow2.com) and note your `clientId` and `clientSecret`.

2. Create an `Allow2Client` and wire up the OAuth2 flow:

```csharp
using Allow2.Service;
using Allow2.Service.Storage;
using Allow2.Service.Cache;

var allow2 = new Allow2Client(
    clientId: "YOUR_SERVICE_TOKEN",
    clientSecret: "YOUR_SERVICE_SECRET",
    tokenStorage: new FileTokenStorage("/var/lib/allow2/tokens.json"),
    cache: new MemoryCache());

// Step 1: Redirect user to Allow2 for pairing
var authorizeUrl = allow2.GetAuthorizeUrl(
    userId: currentUserId,
    redirectUri: "https://yourapp.com/allow2/callback",
    state: csrfToken);
// Redirect to authorizeUrl

// Step 2: Handle the callback
var tokens = await allow2.ExchangeCodeAsync(
    userId: currentUserId,
    code: callbackCode,
    redirectUri: "https://yourapp.com/allow2/callback");

// Step 3: Check permissions on every request
var result = await allow2.CheckAsync(currentUserId, new List<object> { 1, 3 }); // Internet + Gaming

if (!result.Allowed)
{
    // Show block page
}
else
{
    var remaining = result.GetRemainingSeconds(1);
    // Proceed normally, optionally showing countdown
}
```

## Key Concept: One Account = One Child

The Service API links a specific user account on your site to exactly one Allow2 child. There is no child selector -- the identity is established at OAuth2 pairing time when the parent selects which child this account belongs to.

This means:

- Each user account on your site maps to one Allow2 child
- The parent performs pairing once per account, selecting the child
- All subsequent permission checks for that account apply to that child automatically
- Parent/admin accounts can be excluded from checking entirely

## OAuth2 Flow

### Step 1: Authorization

Redirect the user to Allow2 so their parent can pair the account:

```csharp
var state = Guid.NewGuid().ToString("N");
// Store state in session for validation

var authorizeUrl = allow2.GetAuthorizeUrl(
    userId: currentUserId,
    redirectUri: "https://yourapp.com/callback",
    state: state);
// Redirect to authorizeUrl
```

### Step 2: Code Exchange

Handle the OAuth2 callback:

```csharp
// Validate state parameter matches stored session value

var tokens = await allow2.ExchangeCodeAsync(
    userId: currentUserId,
    code: callbackCode,
    redirectUri: "https://yourapp.com/callback");
// Tokens are stored automatically via the configured ITokenStorage
```

### Step 3: Token Refresh

Tokens expire. The SDK handles refresh automatically -- when you call `CheckAsync()` or any other method that requires authentication, the SDK detects expired tokens and refreshes them transparently, persisting the updated tokens via your `ITokenStorage`.

## Permission Checking

Check permissions on every page load or API request:

```csharp
// Simple format -- flat list of activity IDs (auto-expanded with log: true)
var result = await allow2.CheckAsync(userId, new List<object> { 1, 3, 8 }); // Internet + Gaming + Screen Time

// Full format -- explicit log flags
var result = await allow2.CheckAsync(userId, new List<object>
{
    new Dictionary<string, object> { ["id"] = 1, ["log"] = true },
    new Dictionary<string, object> { ["id"] = 8, ["log"] = true },
}, "Australia/Sydney");

if (!result.Allowed)
{
    Console.WriteLine($"Blocked! Day type: {result.TodayDayType?.Name}");
    foreach (var activity in result.Activities)
    {
        if (activity.Banned)
            Console.WriteLine($"{activity.Name} is banned");
        else if (!activity.TimeBlockAllowed)
            Console.WriteLine($"{activity.Name} outside allowed hours");
    }
}
else
{
    var remaining = result.GetRemainingSeconds(1);
    // Optionally show countdown in the UI
}
```

### Convenience Check

```csharp
// Returns true only if ALL specified activities are allowed
var allowed = await allow2.IsAllowedAsync(userId, new List<int> { 1, 3 });
```

### Caching

The SDK caches check results internally using your configured `ICacheService`. The default TTL is 60 seconds and can be overridden via the constructor:

```csharp
var allow2 = new Allow2Client(
    clientId: "YOUR_TOKEN",
    clientSecret: "YOUR_SECRET",
    tokenStorage: storage,
    cache: cache,
    cacheTtl: 30); // cache for 30 seconds
```

## Requests

Children can request changes directly from your site. There are three types of request, and the philosophy is simple: the child drives the configuration, the parent just approves or denies.

### More Time

```csharp
var request = await allow2.RequestMoreTimeAsync(
    userId: userId,
    activityId: 3,       // Gaming
    minutes: 30,
    message: "Almost done with this level!");

Console.WriteLine($"Request ID: {request.RequestId}");

// Poll for parent response
var status = await allow2.GetRequestStatusAsync(request.RequestId, request.StatusSecret);

if (status == "approved")
    Console.WriteLine("Approved!");
else if (status == "denied")
    Console.WriteLine("Request denied.");
else
    Console.WriteLine("Still waiting...");
```

### Day Type Change

```csharp
var request = await allow2.RequestDayTypeChangeAsync(
    userId: userId,
    dayTypeId: 2,        // Weekend
    message: "We have a day off school today.");
```

### Ban Lift

```csharp
var request = await allow2.RequestBanLiftAsync(
    userId: userId,
    activityId: 6,       // Social Media
    message: "I finished all my homework. Can the ban be lifted?");
```

## Voice Codes (Offline Approval)

Even though the child is on a website (online), the **parent** may have no internet -- perhaps they are at work with no signal, or their phone is flat. Voice codes let the parent approve a request by reading a short numeric code over the phone or in person.

### Generate a Challenge

```csharp
using Allow2.Service.Models;

var pair = allow2.GenerateVoiceChallenge(
    secret: pairingSecret,
    type: RequestType.MoreTime,
    activityId: 3,       // Gaming
    minutes: 30);        // in 5-min increments

Console.WriteLine($"Challenge code: {pair.Challenge}");
Console.WriteLine("Read this to your parent. Ask them for the response code.");
```

### Verify the Response

```csharp
var isValid = allow2.VerifyVoiceResponse(
    secret: pairingSecret,
    challenge: pair.Challenge,
    response: parentResponseCode);

if (isValid)
    Console.WriteLine("Approved! Extra time granted.");
else
    Console.WriteLine("Invalid code. Please try again.");
```

The codes use HMAC-SHA256 challenge-response, date-bound (expires at midnight). The format is compact enough to read over a phone call: a spaced challenge and a 6-digit response.

## Feedback

Let users submit bug reports and feature requests directly to you, the developer:

```csharp
using Allow2.Service.Models;

// Submit feedback -- returns the discussion ID
var discussionId = await allow2.SubmitFeedbackAsync(
    userId: userId,
    category: FeedbackCategory.Bug,
    message: "The block page appears even when I have time remaining.");

// Load feedback threads
var threads = await allow2.LoadFeedbackAsync(userId);

// Reply to a thread
await allow2.ReplyToFeedbackAsync(userId, discussionId, "This happens every Tuesday.");
```

## Architecture

| Module | Purpose |
|--------|---------|
| **Allow2Client** | Main entry point, orchestrates all operations |
| **OAuth2Manager** | OAuth2 authorize, code exchange, token refresh |
| **PermissionChecker** | Permission checks with caching |
| **RequestManager** | All 3 request types with temp token + status polling |
| **VoiceCode** | HMAC-SHA256 challenge-response for offline approval |
| **FeedbackManager** | Submit, load, and reply to feedback threads |

### Models

| Model | Purpose |
|-------|---------|
| `CheckResult` | Parsed permission check response with per-activity status |
| `Activity` | Single activity's allowed/blocked state and remaining time |
| `DayType` | Current and upcoming day type information |
| `OAuthTokens` | Access token, refresh token, expiry |
| `RequestResult` | Request ID, status secret, and status with helper methods |
| `VoiceCodePair` | Challenge and expected response pair (record) |
| `RequestType` | Enum: `MoreTime`, `DayTypeChange`, `BanLift` |
| `FeedbackCategory` | Enum: `Bug`, `FeatureRequest`, `NotWorking`, `Other` |

### Exceptions

| Exception | When |
|-----------|------|
| `Allow2Exception` | Base exception for all SDK errors |
| `ApiException` | HTTP or API-level errors |
| `TokenExpiredException` | Token refresh failed (re-pairing needed) |
| `UnpairedException` | No valid tokens for this user (401/403 from API) |

## Token Storage

The SDK persists OAuth2 tokens automatically via the `ITokenStorage` you provide at construction. Two built-in adapters are included.

### MemoryTokenStorage (development/testing)

```csharp
using Allow2.Service.Storage;

var tokenStorage = new MemoryTokenStorage();
```

### FileTokenStorage

```csharp
using Allow2.Service.Storage;

var tokenStorage = new FileTokenStorage("/var/lib/allow2/tokens.json");
```

### Custom Storage (Entity Framework, etc.)

Implement `ITokenStorage` to integrate with your framework:

```csharp
using Allow2.Service;
using Allow2.Service.Models;

public class EfTokenStorage : ITokenStorage
{
    private readonly AppDbContext _db;

    public EfTokenStorage(AppDbContext db) => _db = db;

    public async Task StoreAsync(string userId, OAuthTokens tokens, CancellationToken ct = default)
    {
        var entity = await _db.Allow2Tokens.FindAsync(new object[] { userId }, ct);
        if (entity == null)
        {
            entity = new Allow2TokenEntity { UserId = userId };
            _db.Allow2Tokens.Add(entity);
        }
        entity.AccessToken = tokens.AccessToken;
        entity.RefreshToken = tokens.RefreshToken;
        entity.ExpiresAt = tokens.ExpiresAt;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<OAuthTokens?> RetrieveAsync(string userId, CancellationToken ct = default)
    {
        var entity = await _db.Allow2Tokens.FindAsync(new object[] { userId }, ct);
        if (entity == null) return null;
        return new OAuthTokens(entity.AccessToken, entity.RefreshToken, entity.ExpiresAt);
    }

    public async Task DeleteAsync(string userId, CancellationToken ct = default)
    {
        var entity = await _db.Allow2Tokens.FindAsync(new object[] { userId }, ct);
        if (entity != null)
        {
            _db.Allow2Tokens.Remove(entity);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> ExistsAsync(string userId, CancellationToken ct = default)
    {
        return await _db.Allow2Tokens.AnyAsync(t => t.UserId == userId, ct);
    }
}
```

## License

Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

See [LICENSE](LICENSE) for details.
