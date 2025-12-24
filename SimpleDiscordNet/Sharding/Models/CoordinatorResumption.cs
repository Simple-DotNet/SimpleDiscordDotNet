using System.Text.Json.Serialization;

namespace SimpleDiscordNet.Sharding.Models;

/// <summary>
/// Message sent by a recovered original coordinator to reclaim its role.
/// </summary>
[method: JsonConstructor]
internal sealed record CoordinatorResumptionRequest(
    [property: JsonPropertyName("original_coordinator_id")] string OriginalCoordinatorId,
    [property: JsonPropertyName("original_coordinator_url")] string OriginalCoordinatorUrl,
    [property: JsonPropertyName("timestamp")] long Timestamp
);

/// <summary>
/// Response from temporary coordinator with full cluster state for handoff.
/// </summary>
[method: JsonConstructor]
internal sealed record CoordinatorHandoffData(
    [property: JsonPropertyName("succession_order")] List<SuccessionEntry> SuccessionOrder,
    [property: JsonPropertyName("peer_nodes")] List<PeerNodeState> PeerNodes,
    [property: JsonPropertyName("shard_assignments")] Dictionary<int, string> ShardAssignments,
    [property: JsonPropertyName("total_shards")] int TotalShards,
    [property: JsonPropertyName("cluster_state")] ClusterState ClusterState,
    [property: JsonPropertyName("timestamp")] long Timestamp
);

/// <summary>
/// Message broadcast to all workers when coordinator resumes.
/// </summary>
[method: JsonConstructor]
internal sealed record CoordinatorResumedAnnouncement(
    [property: JsonPropertyName("resumed_coordinator_id")] string ResumedCoordinatorId,
    [property: JsonPropertyName("resumed_coordinator_url")] string ResumedCoordinatorUrl,
    [property: JsonPropertyName("previous_coordinator_id")] string PreviousCoordinatorId,
    [property: JsonPropertyName("succession_order")] List<SuccessionEntry> SuccessionOrder,
    [property: JsonPropertyName("timestamp")] long Timestamp,
    [property: JsonPropertyName("message")] string Message
);
