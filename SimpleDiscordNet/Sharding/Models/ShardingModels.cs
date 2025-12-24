using System.Text.Json.Serialization;

namespace SimpleDiscordNet.Sharding.Models;

/// <summary>
/// Worker registration request sent when joining the cluster.
/// </summary>
[method: JsonConstructor]
internal sealed record WorkerRegistrationRequest(
    [property: JsonPropertyName("process_id")] string ProcessId,
    [property: JsonPropertyName("listen_url")] string ListenUrl,
    [property: JsonPropertyName("max_shards")] int? MaxShards,
    [property: JsonPropertyName("capabilities")] WorkerCapabilities Capabilities,
    [property: JsonPropertyName("timestamp")] long Timestamp
);

/// <summary>
/// Response from coordinator with shard assignments and cluster info.
/// </summary>
[method: JsonConstructor]
internal sealed record WorkerRegistrationResponse(
    [property: JsonPropertyName("assigned_shards")] List<int> AssignedShards,
    [property: JsonPropertyName("total_shards")] int TotalShards,
    [property: JsonPropertyName("succession_position")] int SuccessionPosition,
    [property: JsonPropertyName("succession_order")] List<SuccessionEntry> SuccessionOrder,
    [property: JsonPropertyName("peer_urls")] List<string> PeerUrls,
    [property: JsonPropertyName("coordinator_id")] string CoordinatorId,
    [property: JsonPropertyName("is_original_coordinator")] bool IsOriginalCoordinator,
    [property: JsonPropertyName("timestamp")] long Timestamp
);

/// <summary>
/// Entry in the coordinator succession order.
/// </summary>
[method: JsonConstructor]
internal sealed record SuccessionEntry(
    [property: JsonPropertyName("position")] int Position,
    [property: JsonPropertyName("process_id")] string ProcessId,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("is_original_coordinator")] bool IsOriginalCoordinator
);

/// <summary>
/// Worker capabilities reported during registration.
/// </summary>
[method: JsonConstructor]
internal sealed record WorkerCapabilities(
    [property: JsonPropertyName("memory_mb")] long MemoryMb,
    [property: JsonPropertyName("cpu_cores")] int CpuCores,
    [property: JsonPropertyName("platform")] string Platform
);

/// <summary>
/// Metrics reported by workers to coordinator periodically.
/// </summary>
[method: JsonConstructor]
internal sealed record WorkerMetrics(
    [property: JsonPropertyName("process_id")] string ProcessId,
    [property: JsonPropertyName("timestamp")] long Timestamp,
    [property: JsonPropertyName("cpu_usage")] double CpuUsage,
    [property: JsonPropertyName("memory_usage_mb")] long MemoryUsageMb,
    [property: JsonPropertyName("shards")] List<ShardMetrics> Shards,
    [property: JsonPropertyName("health")] string Health
);

/// <summary>
/// Metrics for a single shard.
/// </summary>
[method: JsonConstructor]
internal sealed record ShardMetrics(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("guild_count")] int GuildCount,
    [property: JsonPropertyName("gateway_latency")] int GatewayLatency,
    [property: JsonPropertyName("events_per_second")] int EventsPerSecond,
    [property: JsonPropertyName("commands_per_second")] int CommandsPerSecond
);

/// <summary>
/// Health check response from a worker/coordinator.
/// </summary>
[method: JsonConstructor]
internal sealed record HealthCheckResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("shards")] List<int> Shards,
    [property: JsonPropertyName("is_coordinator")] bool IsCoordinator,
    [property: JsonPropertyName("timestamp")] long Timestamp
);

/// <summary>
/// Shard assignment command from coordinator to worker.
/// </summary>
[method: JsonConstructor]
internal sealed record ShardAssignment(
    [property: JsonPropertyName("shards")] List<int> Shards,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("timestamp")] long Timestamp
);

/// <summary>
/// Succession order update broadcast.
/// </summary>
[method: JsonConstructor]
internal sealed record SuccessionUpdate(
    [property: JsonPropertyName("succession")] List<SuccessionEntry> Succession,
    [property: JsonPropertyName("removed_node")] string? RemovedNode,
    [property: JsonPropertyName("added_node")] string? AddedNode,
    [property: JsonPropertyName("timestamp")] long Timestamp
);

/// <summary>
/// Shard migration request.
/// </summary>
[method: JsonConstructor]
internal sealed record ShardMigrationRequest(
    [property: JsonPropertyName("shard_id")] int ShardId,
    [property: JsonPropertyName("from_node")] string FromNode,
    [property: JsonPropertyName("to_node")] string ToNode,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("timestamp")] long Timestamp
);

/// <summary>
/// State of a peer node in the cluster.
/// </summary>
[method: JsonConstructor]
internal sealed record PeerNodeState(
    [property: JsonPropertyName("process_id")] string ProcessId,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("shards")] List<int> Shards,
    [property: JsonPropertyName("max_shards")] int? MaxShards,
    [property: JsonPropertyName("last_heartbeat")] long LastHeartbeat,
    [property: JsonPropertyName("metrics")] WorkerMetrics? Metrics
);

/// <summary>
/// Overall cluster state.
/// </summary>
[method: JsonConstructor]
internal sealed record ClusterState(
    [property: JsonPropertyName("total_shards")] int TotalShards,
    [property: JsonPropertyName("healthy_shards")] int HealthyShards,
    [property: JsonPropertyName("total_guilds")] int TotalGuilds,
    [property: JsonPropertyName("nodes")] List<PeerNodeState> Nodes,
    [property: JsonPropertyName("coordinator_id")] string CoordinatorId,
    [property: JsonPropertyName("timestamp")] long Timestamp
);
