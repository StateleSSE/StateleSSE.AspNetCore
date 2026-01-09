# StateleSSE.AspNetCore

ASP.NET Core extension methods for SSE endpoints with zero boilerplate.

## Installation

```bash
dotnet add package StateleSSE.AspNetCore
```

## Usage

```csharp
using StateleSSE.AspNetCore;
using StateleSSE.Abstractions;

[ApiController]
public class GameController(ISseBackplane backplane) : ControllerBase
{
    [HttpGet("events/player-joined")]
    public async Task StreamPlayerJoined([FromQuery] string gameId)
    {
        var channel = ChannelNamingExtensions.Channel<PlayerJoinedEvent>("game", gameId);
        await HttpContext.StreamSseAsync<PlayerJoinedEvent>(backplane, channel);
    }

    [HttpGet("game/stream")]
    public async Task GameStream([FromQuery] string gameId)
    {
        var channel = ChannelNamingExtensions.Channel("game", gameId);
        await HttpContext.StreamSseWithInitialStateAsync(
            backplane, channel, () => GetGameState(gameId), "game_state");
    }
}
```

## Extension Methods

**StreamSseAsync&lt;TEvent&gt;** - Stream typed events
```csharp
await HttpContext.StreamSseAsync<PlayerJoinedEvent>(backplane, channel);
```

**StreamSseWithInitialStateAsync&lt;TState&gt;** - Stream with initial state
```csharp
await HttpContext.StreamSseWithInitialStateAsync(
    backplane, channel, getInitialState, eventName);
```

**StreamSseAsync** - Stream untyped events
```csharp
await HttpContext.StreamSseAsync(backplane, channel);
```

## What It Handles

- SSE response headers
- Backplane subscription lifecycle
- JSON serialization and SSE formatting
- Cancellation token handling
- Cleanup in finally blocks

## Channel Naming

```csharp
ChannelNamingExtensions.Channel<PlayerJoinedEvent>("game", "123");
// "game:123:PlayerJoinedEvent"

ChannelNamingExtensions.Channel("game", "123");
// "game:123"

ChannelNamingExtensions.BroadcastChannel("game");
// "game:all"
```

## License

MIT
