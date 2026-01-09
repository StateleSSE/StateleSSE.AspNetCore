using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace StateleSSE.AspNetCore;

/// <summary>
/// Extension methods for simplifying Server-Sent Events (SSE) streaming with any ISseBackplane implementation.
/// Eliminates boilerplate for headers, subscription management, and cleanup.
/// </summary>
public static class SseStreamingExtensions
{
    /// <summary>
    /// Streams Server-Sent Events from a backplane channel to the HTTP response.
    /// Automatically handles SSE headers, subscription lifecycle, and proper cleanup.
    /// </summary>
    /// <typeparam name="TEvent">The type of events to stream. Must be a class.</typeparam>
    /// <param name="context">The HTTP context.</param>
    /// <param name="backplane">The SSE backplane implementation to subscribe to.</param>
    /// <param name="channel">The channel name to subscribe to (e.g., "game:123:PlayerJoinedEvent").</param>
    /// <param name="cancellationToken">Optional cancellation token. Defaults to RequestAborted.</param>
    /// <returns>A task that completes when the SSE stream ends.</returns>
    public static async Task StreamSseAsync<TEvent>(
        this HttpContext context,
        ISseBackplane backplane,
        string channel,
        CancellationToken cancellationToken = default) where TEvent : class
    {
        cancellationToken = cancellationToken == default ? context.RequestAborted : cancellationToken;

        context.Response.Headers.Append("Content-Type", "text/event-stream");
        context.Response.Headers.Append("Cache-Control", "no-cache");
        context.Response.Headers.Append("Connection", "keep-alive");

        var (reader, subscriberId) = backplane.Subscribe(channel);

        try
        {
            await foreach (var message in reader.ReadAllAsync(cancellationToken))
            {
                if (message is TEvent typedEvent)
                {
                    var json = JsonSerializer.Serialize(typedEvent);
                    await context.Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                    await context.Response.Body.FlushAsync(cancellationToken);
                }
            }
        }
        finally
        {
            backplane.Unsubscribe(channel, subscriberId);
        }
    }

    /// <summary>
    /// Streams Server-Sent Events with an initial state message sent before streaming begins.
    /// Useful for ensuring clients receive current state immediately upon connection.
    /// </summary>
    /// <typeparam name="TState">The type of the initial state. Must be a class.</typeparam>
    /// <param name="context">The HTTP context.</param>
    /// <param name="backplane">The SSE backplane implementation to subscribe to.</param>
    /// <param name="channel">The channel name to subscribe to.</param>
    /// <param name="getInitialState">Function to retrieve the initial state.</param>
    /// <param name="eventName">Optional event name/type for the initial state message.</param>
    /// <param name="cancellationToken">Optional cancellation token. Defaults to RequestAborted.</param>
    /// <returns>A task that completes when the SSE stream ends.</returns>
    public static async Task StreamSseWithInitialStateAsync<TState>(
        this HttpContext context,
        ISseBackplane backplane,
        string channel,
        Func<Task<TState>> getInitialState,
        string? eventName = null,
        CancellationToken cancellationToken = default) where TState : class
    {
        cancellationToken = cancellationToken == default ? context.RequestAborted : cancellationToken;

        context.Response.Headers.Append("Content-Type", "text/event-stream");
        context.Response.Headers.Append("Cache-Control", "no-cache");
        context.Response.Headers.Append("Connection", "keep-alive");

        var (reader, subscriberId) = backplane.Subscribe(channel);

        try
        {
            var initialState = await getInitialState();

            object messageToSend = eventName != null
                ? new SseEventEnvelope<TState>(eventName, initialState)
                : initialState;

            var stateJson = JsonSerializer.Serialize(messageToSend);
            await context.Response.WriteAsync($"data: {stateJson}\n\n", cancellationToken);
            await context.Response.Body.FlushAsync(cancellationToken);

            await foreach (var message in reader.ReadAllAsync(cancellationToken))
            {
                var json = JsonSerializer.Serialize(message);
                await context.Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                await context.Response.Body.FlushAsync(cancellationToken);
            }
        }
        finally
        {
            backplane.Unsubscribe(channel, subscriberId);
        }
    }

    /// <summary>
    /// Streams untyped Server-Sent Events from a backplane channel.
    /// All messages are serialized as received without type filtering.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="backplane">The SSE backplane implementation to subscribe to.</param>
    /// <param name="channel">The channel name to subscribe to.</param>
    /// <param name="cancellationToken">Optional cancellation token. Defaults to RequestAborted.</param>
    /// <returns>A task that completes when the SSE stream ends.</returns>
    public static async Task StreamSseAsync(
        this HttpContext context,
        ISseBackplane backplane,
        string channel,
        CancellationToken cancellationToken = default)
    {
        cancellationToken = cancellationToken == default ? context.RequestAborted : cancellationToken;

        context.Response.Headers.Append("Content-Type", "text/event-stream");
        context.Response.Headers.Append("Cache-Control", "no-cache");
        context.Response.Headers.Append("Connection", "keep-alive");

        var (reader, subscriberId) = backplane.Subscribe(channel);

        try
        {
            await foreach (var message in reader.ReadAllAsync(cancellationToken))
            {
                var json = JsonSerializer.Serialize(message);
                await context.Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                await context.Response.Body.FlushAsync(cancellationToken);
            }
        }
        finally
        {
            backplane.Unsubscribe(channel, subscriberId);
        }
    }
}

/// <summary>
/// Generic envelope for wrapping SSE events with a type/name field.
/// </summary>
public record SseEventEnvelope<T>(string Type, T Data);
