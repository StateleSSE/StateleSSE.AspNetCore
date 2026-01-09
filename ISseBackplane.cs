using System.Threading.Channels;

namespace StateleSSE.AspNetCore;

/// <summary>
/// Abstraction for a Server-Sent Events (SSE) backplane.
/// Enables horizontal scaling by synchronizing events across multiple server instances.
/// Implementations can use Redis, in-memory, RabbitMQ, or any other messaging system.
/// </summary>
public interface ISseBackplane
{
    /// <summary>
    /// Subscribe a local client (SSE connection) to a group.
    /// Returns a channel reader for receiving events and a unique subscriber ID.
    /// </summary>
    /// <param name="groupId">The group identifier (e.g., "game:123", "weather:station1")</param>
    /// <returns>Tuple of channel reader and subscriber ID</returns>
    (ChannelReader<object> reader, Guid subscriberId) Subscribe(string groupId);

    /// <summary>
    /// Unsubscribe a local client when the SSE connection closes.
    /// Performs cleanup and removes the subscriber from the group.
    /// </summary>
    /// <param name="groupId">The group identifier</param>
    /// <param name="subscriberId">The unique subscriber ID from Subscribe()</param>
    void Unsubscribe(string groupId, Guid subscriberId);

    /// <summary>
    /// Publish an event to all subscribers in a specific group across all server instances.
    /// </summary>
    /// <param name="groupId">The group identifier to publish to</param>
    /// <param name="message">The event message to send</param>
    Task PublishToGroup(string groupId, object message);

    /// <summary>
    /// Publish an event to multiple groups simultaneously across all server instances.
    /// More efficient than calling PublishToGroup() multiple times.
    /// </summary>
    /// <param name="groupIds">Collection of group identifiers</param>
    /// <param name="message">The event message to send</param>
    Task PublishToGroups(IEnumerable<string> groupIds, object message);

    /// <summary>
    /// Broadcast an event to all groups and all subscribers across all server instances.
    /// </summary>
    /// <param name="message">The event message to broadcast</param>
    Task PublishToAll(object message);

    /// <summary>
    /// Get the number of local subscribers for a specific group on this server instance.
    /// </summary>
    /// <param name="groupId">The group identifier</param>
    /// <returns>Number of local subscribers</returns>
    int GetLocalSubscriberCount(string groupId);

    /// <summary>
    /// Get all group IDs that have active subscribers on this server instance.
    /// </summary>
    /// <returns>Collection of group IDs</returns>
    IEnumerable<string> GetLocalGroups();

    /// <summary>
    /// Get diagnostic information about the backplane state on this server instance.
    /// Useful for monitoring and debugging.
    /// </summary>
    /// <returns>Diagnostic information</returns>
    BackplaneDiagnostics GetDiagnostics();
}

/// <summary>
/// Diagnostic information about backplane state on a server instance.
/// </summary>
public record BackplaneDiagnostics
{
    /// <summary>
    /// Total number of groups with active subscribers on this server
    /// </summary>
    public required int TotalGroups { get; init; }

    /// <summary>
    /// Total number of local subscribers across all groups on this server
    /// </summary>
    public required int TotalLocalSubscribers { get; init; }

    /// <summary>
    /// Detailed information about each group
    /// </summary>
    public required GroupInfo[] Groups { get; init; }
}

/// <summary>
/// Information about a specific group on this server instance.
/// </summary>
public record GroupInfo
{
    /// <summary>
    /// The group identifier
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// Number of local subscribers in this group on this server
    /// </summary>
    public required int LocalSubscribers { get; init; }
}
