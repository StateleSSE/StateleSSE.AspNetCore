namespace StateleSSE.AspNetCore;

/// <summary>
/// Extension methods for standardized channel naming conventions.
/// Provides type-safe helpers for constructing Redis backplane channel names.
/// </summary>
public static class ChannelNamingExtensions
{
    /// <summary>
    /// Constructs a domain-scoped channel name with an event type suffix.
    /// Pattern: "{domain}:{identifier}:{eventType}"
    /// Example: "game:abc123:PlayerJoinedEvent"
    /// </summary>
    /// <param name="domain">The domain/context (e.g., "game", "weather").</param>
    /// <param name="identifier">The specific identifier (e.g., gameId, stationId).</param>
    /// <param name="eventType">The event type name.</param>
    /// <returns>The formatted channel name.</returns>
    public static string Channel(string domain, string identifier, string eventType)
        => $"{domain}:{identifier}:{eventType}";

    /// <summary>
    /// Constructs a domain-scoped channel name with an event type suffix using generic type.
    /// Pattern: "{domain}:{identifier}:{TEvent}"
    /// Example: Channel&lt;PlayerJoinedEvent&gt;("game", "abc123") => "game:abc123:PlayerJoinedEvent"
    /// </summary>
    /// <typeparam name="TEvent">The event type. The type name will be used as the suffix.</typeparam>
    /// <param name="domain">The domain/context (e.g., "game", "weather").</param>
    /// <param name="identifier">The specific identifier (e.g., gameId, stationId).</param>
    /// <returns>The formatted channel name.</returns>
    public static string Channel<TEvent>(string domain, string identifier)
        => $"{domain}:{identifier}:{typeof(TEvent).Name}";

    /// <summary>
    /// Constructs a simple domain-scoped channel name without event type.
    /// Pattern: "{domain}:{identifier}"
    /// Example: "game:abc123"
    /// </summary>
    /// <param name="domain">The domain/context (e.g., "game", "weather").</param>
    /// <param name="identifier">The specific identifier (e.g., gameId, stationId).</param>
    /// <returns>The formatted channel name.</returns>
    public static string Channel(string domain, string identifier)
        => $"{domain}:{identifier}";

    /// <summary>
    /// Constructs a broadcast channel for all items in a domain.
    /// Pattern: "{domain}:all"
    /// Example: "weather:all"
    /// </summary>
    /// <param name="domain">The domain/context.</param>
    /// <returns>The formatted broadcast channel name.</returns>
    public static string BroadcastChannel(string domain)
        => $"{domain}:all";
}
