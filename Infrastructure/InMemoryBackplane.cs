using System.Collections.Concurrent;
using System.Threading.Channels;
using StateleSSE.AspNetCore;

namespace StateleSSE.AspNetCore.Infrastructure;

/// <summary>
/// In-memory implementation of ISseBackplane for single-server deployments.
/// Ideal for development, testing, and applications that don't require horizontal scaling.
/// All publish operations directly forward to local subscribers without external messaging.
/// </summary>
public class InMemoryBackplane : ISseBackplane, IDisposable
{
    // Local SSE connections
    // groupId -> subscriberId -> channel
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Channel<object>>> _localSubscribers = new();

    public InMemoryBackplane()
    {
        Console.WriteLine("[InMemoryBackplane] Initialized");
    }

    // ============================================
    // CLIENT SUBSCRIPTION (LOCAL SSE CONNECTIONS)
    // ============================================

    /// <summary>
    /// Subscribe a local client (SSE connection) to a group.
    /// Returns channel reader and unique subscriber ID.
    /// </summary>
    public (ChannelReader<object> reader, Guid subscriberId) Subscribe(string groupId)
    {
        var channel = Channel.CreateUnbounded<object>();
        var subscriberId = Guid.NewGuid();

        var channels = _localSubscribers.GetOrAdd(groupId, _ => new ConcurrentDictionary<Guid, Channel<object>>());
        channels.TryAdd(subscriberId, channel);

        Console.WriteLine($"[InMemoryBackplane] New subscriber {subscriberId} for group '{groupId}'. Total local: {channels.Count}");

        return (channel.Reader, subscriberId);
    }

    /// <summary>
    /// Unsubscribe a local client (cleanup when SSE connection closes).
    /// </summary>
    public void Unsubscribe(string groupId, Guid subscriberId)
    {
        if (_localSubscribers.TryGetValue(groupId, out var channels))
        {
            if (channels.TryRemove(subscriberId, out var channel))
            {
                channel.Writer.Complete();
                Console.WriteLine($"[InMemoryBackplane] Unsubscribed {subscriberId} from group '{groupId}'. Remaining local: {channels.Count}");
            }

            // Cleanup: Remove group entry if no subscribers left
            if (channels.IsEmpty)
            {
                _localSubscribers.TryRemove(groupId, out _);
                Console.WriteLine($"[InMemoryBackplane] No subscribers left for group '{groupId}', cleaning up");
            }
        }
    }

    // ============================================
    // BROADCASTING (IN-MEMORY ONLY)
    // ============================================

    /// <summary>
    /// Publish message to all subscribers in a group.
    /// Since this is in-memory only, directly forwards to local subscribers.
    /// </summary>
    public async Task PublishToGroup(string groupId, object message)
    {
        if (_localSubscribers.TryGetValue(groupId, out var channels))
        {
            Console.WriteLine($"[InMemoryBackplane] Publishing to {channels.Count} subscribers in group '{groupId}': {message.GetType().Name}");

            var tasks = channels.Values.Select(channel =>
                channel.Writer.WriteAsync(message).AsTask()
            );

            await Task.WhenAll(tasks);
        }
        else
        {
            Console.WriteLine($"[InMemoryBackplane] Published to group '{groupId}', but no subscribers");
        }
    }

    /// <summary>
    /// Publish message to multiple groups at once.
    /// </summary>
    public async Task PublishToGroups(IEnumerable<string> groupIds, object message)
    {
        var tasks = groupIds.Select(groupId => PublishToGroup(groupId, message));
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Publish message to ALL groups (broadcast to entire system).
    /// Since this is in-memory, broadcasts to all local subscribers.
    /// </summary>
    public async Task PublishToAll(object message)
    {
        Console.WriteLine($"[InMemoryBackplane] Broadcasting to ALL groups: {message.GetType().Name}");

        var allTasks = new List<Task>();

        foreach (var (groupId, channels) in _localSubscribers)
        {
            Console.WriteLine($"[InMemoryBackplane] Broadcasting to {channels.Count} subscribers in group '{groupId}'");

            var tasks = channels.Values.Select(channel =>
                channel.Writer.WriteAsync(message).AsTask()
            );

            allTasks.AddRange(tasks);
        }

        await Task.WhenAll(allTasks);
    }

    // ============================================
    // STATS & DIAGNOSTICS
    // ============================================

    /// <summary>
    /// Get count of subscribers for a group.
    /// </summary>
    public int GetLocalSubscriberCount(string groupId)
    {
        return _localSubscribers.TryGetValue(groupId, out var channels) ? channels.Count : 0;
    }

    /// <summary>
    /// Get all group IDs that have active subscribers.
    /// </summary>
    public IEnumerable<string> GetLocalGroups()
    {
        return _localSubscribers.Keys;
    }

    /// <summary>
    /// Get diagnostic info about the backplane state.
    /// </summary>
    public BackplaneDiagnostics GetDiagnostics()
    {
        return new BackplaneDiagnostics
        {
            TotalGroups = _localSubscribers.Count,
            TotalLocalSubscribers = _localSubscribers.Values.Sum(c => c.Count),
            Groups = _localSubscribers.Select(kvp => new GroupInfo
            {
                GroupId = kvp.Key,
                LocalSubscribers = kvp.Value.Count
            }).ToArray()
        };
    }

    // ============================================
    // CLEANUP
    // ============================================

    public void Dispose()
    {
        // Complete all local channels
        foreach (var groupChannels in _localSubscribers.Values)
        {
            foreach (var channel in groupChannels.Values)
            {
                channel.Writer.Complete();
            }
        }

        _localSubscribers.Clear();
        Console.WriteLine("[InMemoryBackplane] Disposed");
    }
}
