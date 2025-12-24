using SimpleDiscordNet.Sharding.Models;

namespace SimpleDiscordNet.Sharding;

/// <summary>
/// Represents a remote worker process in the distributed sharding cluster.
/// Tracks connection state, assigned shards, metrics, and health status.
/// Example: var peer = new PeerNode("worker-1", "http://192.168.1.10:8080");
/// </summary>
internal sealed class PeerNode
{
    private long _lastHeartbeat;
    private readonly object _heartbeatLock = new();
    private volatile WorkerMetrics? _latestMetrics;

    public string ProcessId { get; }
    public string Url { get; }
    public List<int> AssignedShards { get; } = [];
    public int? MaxShards { get; set; }

    public PeerNode(string processId, string url)
    {
        ProcessId = processId;
        Url = url;
        _lastHeartbeat = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// Updates the last heartbeat timestamp to now.
    /// Example: peer.RecordHeartbeat();
    /// </summary>
    public void RecordHeartbeat()
    {
        lock (_heartbeatLock)
        {
            _lastHeartbeat = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    /// <summary>
    /// Updates the latest metrics from this worker.
    /// Example: peer.UpdateMetrics(metrics);
    /// </summary>
    public void UpdateMetrics(WorkerMetrics metrics)
    {
        _latestMetrics = metrics;
        lock (_heartbeatLock)
        {
            _lastHeartbeat = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    /// <summary>
    /// Gets the milliseconds since last heartbeat.
    /// Example: if (peer.MillisSinceLastHeartbeat > 15000) { ... }
    /// </summary>
    public long MillisSinceLastHeartbeat
    {
        get
        {
            lock (_heartbeatLock)
            {
                return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _lastHeartbeat;
            }
        }
    }

    /// <summary>
    /// Gets whether this peer is considered healthy (heartbeat within 15 seconds).
    /// Example: if (peer.IsHealthy) { ... }
    /// </summary>
    public bool IsHealthy => MillisSinceLastHeartbeat < 15000;

    /// <summary>
    /// Gets the latest metrics from this worker, or null if none received.
    /// Example: var cpuUsage = peer.LatestMetrics?.CpuUsage ?? 0;
    /// </summary>
    public WorkerMetrics? LatestMetrics => _latestMetrics;

    /// <summary>
    /// Converts this peer to a PeerNodeState for serialization.
    /// Example: var state = peer.ToState();
    /// </summary>
    public PeerNodeState ToState()
    {
        long lastHeartbeat;
        lock (_heartbeatLock)
        {
            lastHeartbeat = _lastHeartbeat;
        }

        return new PeerNodeState(
            ProcessId: ProcessId,
            Url: Url,
            Shards: AssignedShards.ToList(),
            MaxShards: MaxShards,
            LastHeartbeat: lastHeartbeat,
            Metrics: _latestMetrics
        );
    }

    /// <summary>
    /// Creates a PeerNode from a serialized PeerNodeState.
    /// Example: var peer = PeerNode.FromState(state);
    /// </summary>
    public static PeerNode FromState(PeerNodeState state)
    {
        PeerNode peer = new(state.ProcessId, state.Url);
        lock (peer._heartbeatLock)
        {
            peer._lastHeartbeat = state.LastHeartbeat;
        }
        peer._latestMetrics = state.Metrics;
        peer.MaxShards = state.MaxShards;
        peer.AssignedShards.AddRange(state.Shards);
        return peer;
    }
}
