# StateleSSE.AspNetCore

ASP.NET Core extension methods for the StateleSSE ecosystem. Eliminates boilerplate code when creating Server-Sent Events (SSE) endpoints.

## Installation

```bash
dotnet add package StateleSSE.AspNetCore
```

## What Problem Does This Solve?

When using an `ISseBackplane` implementation, you typically need repetitive boilerplate for each SSE endpoint:

```csharp
// BEFORE: 27 lines of boilerplate
[HttpGet("game/stream")]
public async Task GameStream([FromQuery] string gameId)
{
    HttpContext.Response.Headers.Append("Content-Type", "text/event-stream");
    HttpContext.Response.Headers.Append("Cache-Control", "no-cache");
    HttpContext.Response.Headers.Append("Connection", "keep-alive");

    var (reader, subscriberId) = backplane.Subscribe($"game:{gameId}");

    var gameState = await GetGameState(gameId);
    var envelope = new SseEventEnvelope<GameStateResponse>("game_state", gameState);
    var stateJson = JsonSerializer.Serialize(envelope);
    await HttpContext.Response.WriteAsync($"data: {stateJson}\n\n");
    await HttpContext.Response.Body.FlushAsync();

    try
    {
        await foreach (var message in reader.ReadAllAsync(HttpContext.RequestAborted))
        {
            var json = JsonSerializer.Serialize(message);
            await HttpContext.Response.WriteAsync($"data: {json}\n\n");
            await HttpContext.Response.Body.FlushAsync();
        }
    }
    finally
    {
        backplane.Unsubscribe($"game:{gameId}", subscriberId);
    }
}
```

This library reduces it to:

```csharp
// AFTER: 7 lines, zero boilerplate
[HttpGet("game/stream")]
public async Task GameStream([FromQuery] string gameId)
{
    var channel = ChannelNamingExtensions.Channel("game", gameId);
    await HttpContext.StreamSseWithInitialStateAsync(
        backplane, channel, () => GetGameState(gameId), "game_state");
}
```

## Features

### 1. HttpContext Extension Methods

Three main extension methods for different SSE patterns:

#### StreamSseAsync&lt;TEvent&gt; - Typed Event Streaming

Stream events of a specific type without initial state:

```csharp
using StateleSSE.AspNetCore;
using StateleSSE.Abstractions;

[HttpGet("events/player-joined")]
public async Task StreamPlayerJoined([FromQuery] string gameId)
{
    var channel = ChannelNamingExtensions.Channel<PlayerJoinedEvent>("game", gameId);
    await HttpContext.StreamSseAsync<PlayerJoinedEvent>(backplane, channel);
}
```

#### StreamSseWithInitialStateAsync&lt;TState&gt; - With Initial State

Send an initial state message before streaming begins (useful for "catch-up" patterns):

```csharp
[HttpGet("game/stream")]
public async Task GameStream([FromQuery] string gameId)
{
    var channel = ChannelNamingExtensions.Channel("game", gameId);
    await HttpContext.StreamSseWithInitialStateAsync(
        backplane,
        channel,
        getInitialState: () => GetGameState(gameId),
        eventName: "game_state");
}
```

The initial state will be wrapped in an envelope: `{ "Type": "game_state", "Data": {...} }`

#### StreamSseAsync - Untyped Streaming

Stream any event type without filtering:

```csharp
[HttpGet("events/all")]
public async Task StreamAllEvents([FromQuery] string gameId)
{
    var channel = ChannelNamingExtensions.Channel("game", gameId);
    await HttpContext.StreamSseAsync(backplane, channel);
}
```

### 2. Channel Naming Helpers

Type-safe helpers for standardized channel naming:

```csharp
using StateleSSE.AspNetCore;

// Pattern: "{domain}:{identifier}:{EventType}"
var channel1 = ChannelNamingExtensions.Channel<PlayerJoinedEvent>("game", "abc123");
// Result: "game:abc123:PlayerJoinedEvent"

// Pattern: "{domain}:{identifier}:{eventType}"
var channel2 = ChannelNamingExtensions.Channel("game", "abc123", "PlayerJoinedEvent");
// Result: "game:abc123:PlayerJoinedEvent"

// Pattern: "{domain}:{identifier}"
var channel3 = ChannelNamingExtensions.Channel("game", "abc123");
// Result: "game:abc123"

// Pattern: "{domain}:all" (broadcast)
var channel4 = ChannelNamingExtensions.BroadcastChannel("weather");
// Result: "weather:all"
```

## What the Library Handles Automatically

- ✅ SSE response headers (`Content-Type: text/event-stream`, `Cache-Control: no-cache`, etc.)
- ✅ Backplane subscription lifecycle (`Subscribe`/`Unsubscribe`)
- ✅ Proper cleanup in `finally` blocks
- ✅ JSON serialization of events
- ✅ SSE message formatting (`data: {...}\n\n`)
- ✅ Response flushing
- ✅ Cancellation token handling (`HttpContext.RequestAborted`)
- ✅ Initial state serialization and envelope wrapping

## Complete Example

```csharp
using StateleSSE.AspNetCore;
using StateleSSE.Abstractions;
using Microsoft.AspNetCore.Mvc;

[ApiController]
public class GameEventsController(ISseBackplane backplane) : ControllerBase
{
    // Typed event streaming (no initial state)
    [HttpGet("events/player-joined")]
    public async Task StreamPlayerJoined([FromQuery] string gameId)
    {
        var channel = ChannelNamingExtensions.Channel<PlayerJoinedEvent>("game", gameId);
        await HttpContext.StreamSseAsync<PlayerJoinedEvent>(backplane, channel);
    }

    // Untyped streaming with initial state
    [HttpGet("game/stream")]
    public async Task GameStream([FromQuery] string gameId)
    {
        var channel = ChannelNamingExtensions.Channel("game", gameId);
        await HttpContext.StreamSseWithInitialStateAsync(
            backplane,
            channel,
            () => GetGameState(gameId),
            "game_state");
    }

    // Broadcast to all games
    [HttpGet("games/all")]
    public async Task StreamAllGames()
    {
        var channel = ChannelNamingExtensions.BroadcastChannel("game");
        await HttpContext.StreamSseAsync(backplane, channel);
    }

    private async Task<GameStateResponse> GetGameState(string gameId)
    {
        // Your logic here
        throw new NotImplementedException();
    }
}
```

## When to Use Each Method

| Method | Use Case | Initial State | Type Filtering |
|--------|----------|---------------|----------------|
| `StreamSseAsync<TEvent>` | Single event type per endpoint (recommended) | ❌ No | ✅ Yes |
| `StreamSseWithInitialStateAsync<TState>` | Client needs current state on connect | ✅ Yes | ❌ No |
| `StreamSseAsync` (untyped) | Multiple event types on one endpoint | ❌ No | ❌ No |

## Backplane Implementations

This library works with any `ISseBackplane` implementation:

```bash
# Redis backplane (production horizontal scaling)
dotnet add package StateleSSE.Backplane.Redis

# In-memory backplane (single server, dev/test)
dotnet add package StateleSSE.Backplane.InMemory
```

**Registration:**

```csharp
using StateleSSE.Backplane.Redis.Infrastructure;
using StackExchange.Redis;

// Redis backplane
var redis = ConnectionMultiplexer.Connect("localhost:6379");
builder.Services.AddSingleton<ISseBackplane>(sp =>
    new RedisBackplane(redis, channelPrefix: "myapp"));

// Or custom backplane
builder.Services.AddSingleton<ISseBackplane, MyCustomBackplane>();
```

## Architecture Recommendations

For **stateless documentation-driven SSE**, use this library with:

1. **StateleSSE.CodeGen.TypeScript** - Generates TypeScript EventSource clients from your DTOs
2. **StateleSSE.Backplane.Redis** - Provides horizontal scaling via Redis pub/sub
3. **StateleSSE.AspNetCore** (this library) - Eliminates endpoint boilerplate

### Recommended Pattern

```csharp
// 1. Define your event DTOs
public record PlayerJoinedEvent(
    string UserId,
    string UserName,
    int PlayerCount,
    DateTime Timestamp)
{
    public string Type => "player_joined";
}

// 2. Create typed SSE endpoints with zero boilerplate
[HttpGet(nameof(PlayerJoinedEvent))]
[EventSourceEndpoint(typeof(PlayerJoinedEvent))]  // For CodeGen
public async Task StreamPlayerJoined([FromQuery] string gameId)
{
    var channel = ChannelNamingExtensions.Channel<PlayerJoinedEvent>("game", gameId);
    await HttpContext.StreamSseAsync<PlayerJoinedEvent>(backplane, channel);
}

// 3. Publish events from your business logic
await backplane.PublishToGroup(
    $"game:{gameId}:PlayerJoinedEvent",
    new PlayerJoinedEvent(userId, userName, playerCount, DateTime.UtcNow)
);

// 4. Generate TypeScript client (in Program.cs)
using StateleSSE.CodeGen.TypeScript;

TypeScriptSseGenerator.Generate(
    openApiSpecPath: "openapi-with-docs.json",
    outputPath: "../../client/src/generated-sse-client.ts"
);
```

## Extension Method Signatures

```csharp
public static class SseStreamingExtensions
{
    // Typed streaming
    public static async Task StreamSseAsync<TEvent>(
        this HttpContext context,
        ISseBackplane backplane,
        string channel,
        CancellationToken cancellationToken = default) where TEvent : class

    // With initial state
    public static async Task StreamSseWithInitialStateAsync<TState>(
        this HttpContext context,
        ISseBackplane backplane,
        string channel,
        Func<Task<TState>> getInitialState,
        string? eventName = null,
        CancellationToken cancellationToken = default) where TState : class

    // Untyped streaming
    public static async Task StreamSseAsync(
        this HttpContext context,
        ISseBackplane backplane,
        string channel,
        CancellationToken cancellationToken = default)
}
```

## Related Packages

| Package | Purpose |
|---------|---------|
| `StateleSSE.Abstractions` | Core `ISseBackplane` interface |
| `StateleSSE.Backplane.Redis` | Redis backplane for horizontal scaling |
| `StateleSSE.CodeGen.TypeScript` | TypeScript EventSource client generation |

## License

MIT
