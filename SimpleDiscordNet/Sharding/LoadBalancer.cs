using System.Collections.Concurrent;
using SimpleDiscordNet.Logging;
using SimpleDiscordNet.Sharding.Models;

namespace SimpleDiscordNet.Sharding;

/// <summary>
/// Analyzes worker metrics and performs automatic shard migration to balance load.
/// Triggers migration when CPU usage exceeds 80% or latency exceeds 500ms.
/// Example: var balancer = new LoadBalancer(peers, OnMigrationNeeded, logger); balancer.AnalyzeAndBalance();
/// </summary>
internal sealed class LoadBalancer
{
    private readonly ConcurrentDictionary<string, PeerNode> _peers;
    private readonly Action<ShardMigrationRequest> _onMigrationNeeded;
    private readonly NativeLogger _logger;

    private const double HighCpuThreshold = 0.80; // 80%
    private const int HighLatencyThreshold = 500; // 500ms

    public LoadBalancer(ConcurrentDictionary<string, PeerNode> peers, Action<ShardMigrationRequest> onMigrationNeeded, NativeLogger logger)
    {
        _peers = peers;
        _onMigrationNeeded = onMigrationNeeded;
        _logger = logger;
    }

    /// <summary>
    /// Analyzes all worker metrics and triggers migrations if needed.
    /// Example: balancer.AnalyzeAndBalance();
    /// </summary>
    public void AnalyzeAndBalance()
    {
        try
        {
            PeerNode[] workers = _peers.Values.Where(p => p.IsHealthy).ToArray();
            if (workers.Length < 2) return; // Need at least 2 workers to balance

            // Find overloaded workers
            PeerNode[] overloaded = workers.Where(w =>
            {
                WorkerMetrics? metrics = w.LatestMetrics;
                if (metrics == null) return false;

                bool highCpu = metrics.CpuUsage > HighCpuThreshold;
                bool highLatency = metrics.Shards?.Any(s => s.GatewayLatency > HighLatencyThreshold) ?? false;

                return (highCpu || highLatency) && w.AssignedShards.Count > 1;
            }).ToArray();

            if (overloaded.Length == 0) return;

            // Find healthy targets with capacity
            PeerNode[] targets = workers
                .Where(w => !overloaded.Contains(w))
                .Where(w =>
                {
                    WorkerMetrics? metrics = w.LatestMetrics;
                    if (metrics == null) return false;
                    return metrics.CpuUsage < 0.60 && w.AssignedShards.Count < GetMaxShardsPerWorker(workers.Length);
                })
                .OrderBy(w => w.LatestMetrics?.CpuUsage ?? 0)
                .ToArray();

            if (targets.Length == 0)
            {
                _logger.Log(LogLevel.Warning, "LoadBalancer: No healthy target workers available for migration");
                return;
            }

            // Migrate one shard from each overloaded worker
            int targetIndex = 0;
            foreach (PeerNode overloadedWorker in overloaded)
            {
                if (overloadedWorker.AssignedShards.Count == 0) continue;

                // Find the shard with highest latency/load
                WorkerMetrics? metrics = overloadedWorker.LatestMetrics;
                int shardToMigrate;

                if (metrics?.Shards is { Count: > 0 })
                {
                    ShardMetrics worstShard = metrics.Shards.OrderByDescending(s => s.GatewayLatency).First();
                    shardToMigrate = worstShard.Id;
                }
                else
                {
                    // Fallback to first assigned shard
                    shardToMigrate = overloadedWorker.AssignedShards[0];
                }

                PeerNode target = targets[targetIndex % targets.Length];
                targetIndex++;

                PeerNodeState overloadedState = overloadedWorker.ToState();
                PeerNodeState targetState = target.ToState();

                _logger.Log(LogLevel.Information, $"LoadBalancer: Migrating shard {shardToMigrate} from {overloadedState.ProcessId} (CPU: {metrics?.CpuUsage:P0}) to {targetState.ProcessId} (CPU: {target.LatestMetrics?.CpuUsage:P0})");

                ShardMigrationRequest migrationRequest = new ShardMigrationRequest(
                    ShardId: shardToMigrate,
                    FromNode: overloadedState.ProcessId,
                    ToNode: targetState.ProcessId,
                    Reason: $"Load balancing - High load on source worker (CPU: {metrics?.CpuUsage:P0})",
                    Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                );

                _onMigrationNeeded(migrationRequest);
            }
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"LoadBalancer error: {ex.Message}", ex);
        }
    }

    private static int GetMaxShardsPerWorker(int totalWorkers)
    {
        // Allow up to 16 shards per worker, but scale based on worker count
        return totalWorkers switch
        {
            1 => int.MaxValue,
            2 => 8,
            <= 4 => 6,
            <= 8 => 4,
            _ => 2
        };
    }

    /// <summary>
    /// Gets a load distribution summary for all workers.
    /// Example: var summary = balancer.GetLoadSummary();
    /// </summary>
    public string GetLoadSummary()
    {
        PeerNode[] workers = _peers.Values.Where(p => p.IsHealthy).ToArray();
        if (workers.Length == 0) return "No healthy workers";

        List<string> lines = ["Load Distribution:"];
        foreach (PeerNode worker in workers.OrderBy(w => w.ProcessId))
        {
            PeerNodeState state = worker.ToState();
            WorkerMetrics? metrics = worker.LatestMetrics;
            double cpu = metrics?.CpuUsage ?? 0;
            int shardCount = worker.AssignedShards.Count;
            double avgLatency = metrics?.Shards?.Average(s => s.GatewayLatency) ?? 0;

            lines.Add($"  {state.ProcessId}: {shardCount} shards, CPU: {cpu:P0}, Avg Latency: {avgLatency:F0}ms");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
