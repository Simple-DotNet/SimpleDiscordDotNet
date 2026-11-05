using SimpleDiscordNet.Sharding.Models;

namespace SimpleDiscordNet.Sharding;

/// <summary>
/// Decides which worker runs which shard. Callers hold the coordinator's assignment lock.
/// </summary>
internal static class ShardAssigner
{
    private static int GetShardCount(PeerNode peer)
    {
        lock (peer.ShardsLock)
        {
            return peer.AssignedShards.Count;
        }
    }

    private static bool HasCapacity(PeerNode peer)
        => peer.MaxShards is not { } max || GetShardCount(peer) < max;

    /// <summary>
    /// Gives a newly registered worker every unassigned shard, then evens out the cluster by moving
    /// shards to it from the busiest workers. Returns the moves, so the old owners can be told to stop.
    /// </summary>
    public static List<ShardMigrationRequest> AssignNewWorker(IReadOnlyCollection<PeerNode> peers, PeerNode worker, int totalShards)
    {
        HashSet<int> assigned = [];
        foreach (PeerNode peer in peers)
        {
            assigned.UnionWith(peer.GetShardsSnapshot());
        }

        foreach (int shardId in Enumerable.Range(0, totalShards).Where(s => !assigned.Contains(s)))
        {
            if (!HasCapacity(worker)) break;
            lock (worker.ShardsLock)
            {
                worker.AssignedShards.Add(shardId);
            }
        }

        List<ShardMigrationRequest> migrations = [];
        int fairShare = totalShards / Math.Max(1, peers.Count);

        while (GetShardCount(worker) < fairShare && HasCapacity(worker))
        {
            PeerNode? busiest = peers.Where(p => !ReferenceEquals(p, worker)).MaxBy(GetShardCount);
            if (busiest is null || GetShardCount(busiest) <= fairShare) break;

            int shardId;
            lock (busiest.ShardsLock)
            {
                shardId = busiest.AssignedShards[^1];
                busiest.AssignedShards.RemoveAt(busiest.AssignedShards.Count - 1);
            }
            lock (worker.ShardsLock)
            {
                worker.AssignedShards.Add(shardId);
            }

            migrations.Add(new ShardMigrationRequest(
                ShardId: shardId,
                FromNode: busiest.ProcessId,
                ToNode: worker.ProcessId,
                Reason: $"Rebalancing for new worker {worker.ProcessId}",
                Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        }

        return migrations;
    }

    /// <summary>
    /// Hands a failed worker's shards to the least loaded workers. Shards that fit nowhere stay
    /// unassigned, and the next worker to register picks them up.
    /// </summary>
    public static Dictionary<PeerNode, List<int>> ReassignShards(IReadOnlyCollection<int> shards, IReadOnlyCollection<PeerNode> workers)
    {
        Dictionary<PeerNode, List<int>> assignments = [];

        foreach (int shardId in shards)
        {
            PeerNode? target = workers.Where(HasCapacity).MinBy(GetShardCount);
            if (target is null) break;

            lock (target.ShardsLock)
            {
                target.AssignedShards.Add(shardId);
            }

            if (!assignments.TryGetValue(target, out List<int>? targetShards))
            {
                targetShards = [];
                assignments[target] = targetShards;
            }
            targetShards.Add(shardId);
        }

        return assignments;
    }
}
