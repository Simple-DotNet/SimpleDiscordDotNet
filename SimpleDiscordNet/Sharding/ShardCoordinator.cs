using System.Collections.Concurrent;
using System.Text.Json;
using SimpleDiscordNet.Logging;
using SimpleDiscordNet.Sharding.Models;

namespace SimpleDiscordNet.Sharding;

/// <summary>
/// Main coordinator for distributed sharding cluster. Manages worker registration,
/// shard assignment, health monitoring, load balancing, and coordinator resumption.
/// Example: var coordinator = new ShardCoordinator(token, "http://+:8080/", isOriginal: true, logger);
/// </summary>
internal sealed class ShardCoordinator : IDisposable
{
    private readonly string _token;
    private readonly string _listenUrl;
    private readonly bool _isOriginalCoordinator;
    private readonly NativeLogger _logger;
    private readonly ShardHttpServer _server;
    private readonly ShardHttpClient _client;
    private readonly ConcurrentDictionary<string, PeerNode> _peers = new();
    private readonly SuccessionManager _succession;
    private readonly HealthMonitor _healthMonitor;
    private readonly LoadBalancer _loadBalancer;
    private readonly Timer _metricsTimer;
    private readonly Timer _balancingTimer;
    private readonly object _assignmentLock = new();
    private volatile int _totalShards;
    private volatile int _maxConcurrency = 1;
    private volatile bool _disposed;
    private volatile bool _timersDisposed;
    private readonly string _coordinatorId;

    public ShardCoordinator(string token, string listenUrl, NativeLogger logger, bool isOriginalCoordinator = true)
    {
        _token = token;
        _listenUrl = ShardUrl.ToAdvertised(listenUrl);
        _logger = logger;
        _isOriginalCoordinator = isOriginalCoordinator;
        _coordinatorId = $"coordinator-{Guid.NewGuid():N}";
        string clusterSecret = ClusterAuth.DeriveSecret(token);
        _server = new ShardHttpServer(listenUrl, clusterSecret, logger);
        _client = new ShardHttpClient(clusterSecret);
        _succession = new SuccessionManager(logger);
        _healthMonitor = new HealthMonitor(_peers, OnPeerFailed, logger);
        _loadBalancer = new LoadBalancer(_peers, OnMigrationNeeded, logger);
        _metricsTimer = new Timer(RequestMetrics, null, Timeout.Infinite, Timeout.Infinite);
        _balancingTimer = new Timer(RunLoadBalancing, null, Timeout.Infinite, Timeout.Infinite);

        SetupEndpoints();
    }

    private void SetupEndpoints()
    {
        _server.OnRegister(HandleRegisterAsync);
        _server.OnHealth(HandleHealthAsync);
        _server.OnMetrics(HandleMetricsAsync);
        _server.OnClusterState(HandleClusterStateAsync);

        if (_isOriginalCoordinator)
        {
            _server.OnHandoff(HandleHandoffAsync);
        }
        else
        {
            _server.OnResumption(HandleResumptionAsync);
        }
    }

    /// <summary>
    /// Starts the coordinator, auto-detects shard count, and begins accepting workers.
    /// Example: await coordinator.StartAsync(ct);
    /// </summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ShardCoordinator));

        // Auto-detect shard count from Discord
        (_totalShards, _maxConcurrency) = await AutoDetectGatewayLimitsAsync(ct).ConfigureAwait(false);
        _logger.Log(LogLevel.Information, $"Coordinator detected {_totalShards} total shards from Discord (max_concurrency {_maxConcurrency})");

        _server.Start();
        _healthMonitor.Start();
        _balancingTimer.Change(10000, 10000); // Load balancing every 10s

        _logger.Log(LogLevel.Information, $"Coordinator started on {_listenUrl} (Original: {_isOriginalCoordinator})");
    }

    private async Task<(int Shards, int MaxConcurrency)> AutoDetectGatewayLimitsAsync(CancellationToken ct)
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("Authorization", $"Bot {_token}");
            http.DefaultRequestHeaders.UserAgent.ParseAdd(Core.LibraryInfo.UserAgent);

            var response = await http.GetAsync("https://discord.com/api/v10/gateway/bot", ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            return ParseGatewayLimits(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Failed to auto-detect shard count: {ex.Message}. Defaulting to 1.", ex);
            return (1, 1);
        }
    }

    internal static (int Shards, int MaxConcurrency) ParseGatewayLimits(string gatewayBotJson)
    {
        using JsonDocument doc = JsonDocument.Parse(gatewayBotJson);
        JsonElement root = doc.RootElement;

        int maxConcurrency = root.TryGetProperty("session_start_limit", out JsonElement limit) &&
                             limit.TryGetProperty("max_concurrency", out JsonElement concurrency)
            ? concurrency.GetInt32()
            : 1;

        return (root.GetProperty("shards").GetInt32(), maxConcurrency);
    }

    private async Task HandleRegisterAsync(System.Net.HttpListenerContext context)
    {
        var request = await _server.ReadJsonAsync<WorkerRegistrationRequest>(context);
        if (request == null || string.IsNullOrEmpty(request.ProcessId))
        {
            await _server.RespondAsync(context, 400, new HttpErrorResponse { error = "Invalid request" });
            return;
        }

        string workerUrl = ShardUrl.Normalize(request.ListenUrl);
        PeerNode peer = new(request.ProcessId, workerUrl)
        {
            MaxShards = request.MaxShards
        };

        List<ShardMigrationRequest> migrations;
        lock (_assignmentLock)
        {
            // A worker registering again keeps the shards it is already running
            if (_peers.TryGetValue(request.ProcessId, out PeerNode? existing))
            {
                lock (peer.ShardsLock)
                    peer.AssignedShards.AddRange(existing.GetShardsSnapshot());
            }
            _peers[request.ProcessId] = peer;

            // Auto-assign shards
            migrations = ShardAssigner.AssignNewWorker(_peers.Values.ToArray(), peer, _totalShards);
        }

        int position = _succession.AddWorker(request.ProcessId, workerUrl, false);

        // Stop moved shards on their old workers before the new worker starts them
        foreach (ShardMigrationRequest migration in migrations)
        {
            await NotifyMigrationSourceAsync(migration);
        }

        WorkerRegistrationResponse response = new(
            AssignedShards: GetAssignedShardsSafe(peer),
            TotalShards: _totalShards,
            SuccessionPosition: position,
            SuccessionOrder: _succession.GetAll(),
            PeerUrls: _peers.Values.Select(p => p.ToState().Url).ToList(),
            CoordinatorId: _coordinatorId,
            IsOriginalCoordinator: _isOriginalCoordinator,
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            MaxConcurrency: _maxConcurrency
        );

        await _server.RespondAsync(context, 200, response);

        // Broadcast succession update to all peers
        await BroadcastSuccessionUpdateAsync();

        _logger.Log(LogLevel.Information, $"Worker {request.ProcessId} registered with shards: [{string.Join(", ", peer.GetShardsSnapshot())}]");
    }

    private async Task NotifyMigrationSourceAsync(ShardMigrationRequest migration)
    {
        try
        {
            if (_peers.TryGetValue(migration.FromNode, out PeerNode? source))
            {
                await _client.PostAsync($"{source.Url}/migrate", migration).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Failed to stop shard {migration.ShardId} on {migration.FromNode}: {ex.Message}", ex);
        }
    }

    private static List<int> GetAssignedShardsSafe(PeerNode peer)
    {
        lock (peer.ShardsLock)
            return peer.AssignedShards.ToList();
    }

    private async Task HandleHealthAsync(System.Net.HttpListenerContext context)
    {
        HealthCheckResponse response = new(
            Status: "healthy",
            Shards: Enumerable.Range(0, _totalShards).ToList(),
            IsCoordinator: true,
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        );

        await _server.RespondAsync(context, 200, response);
    }

    private async Task HandleMetricsAsync(System.Net.HttpListenerContext context)
    {
        WorkerMetrics? metrics = await _server.ReadJsonAsync<WorkerMetrics>(context);
        if (metrics == null || string.IsNullOrEmpty(metrics.ProcessId))
        {
            await _server.RespondAsync(context, 400, new HttpErrorResponse { error = "Invalid metrics" });
            return;
        }

        if (_peers.TryGetValue(metrics.ProcessId, out var peer))
        {
            peer.UpdateMetrics(metrics);
        }

        await ShardHttpServer.RespondAsync(context, 204);
    }

    private async Task HandleClusterStateAsync(System.Net.HttpListenerContext context)
    {
        int healthyShards = _peers.Values
            .Where(p => p.IsHealthy)
            .SelectMany(p => p.GetShardsSnapshot())
            .Distinct()
            .Count();

        int totalGuilds = _peers.Values
            .Where(p => p.LatestMetrics != null)
            .SelectMany(p => p.LatestMetrics!.Shards)
            .Sum(s => s.GuildCount);

        ClusterState state = new ClusterState(
            TotalShards: _totalShards,
            HealthyShards: healthyShards,
            TotalGuilds: totalGuilds,
            Nodes: _peers.Values.Select(p => p.ToState()).ToList(),
            CoordinatorId: _coordinatorId,
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        );

        await _server.RespondAsync(context, 200, state);
    }

    private async Task HandleResumptionAsync(System.Net.HttpListenerContext context)
    {
        if (_isOriginalCoordinator)
        {
            await _server.RespondAsync(context, 400, new HttpErrorResponse { error = "Already original coordinator" });
            return;
        }

        CoordinatorResumptionRequest? request = await _server.ReadJsonAsync<CoordinatorResumptionRequest>(context);
        if (request == null || string.IsNullOrEmpty(request.OriginalCoordinatorId))
        {
            await _server.RespondAsync(context, 400, new HttpErrorResponse { error = "Invalid request" });
            return;
        }

        _logger.Log(LogLevel.Information, $"Original coordinator {request.OriginalCoordinatorId} requesting resumption");

        // Package handoff data - create shard assignments dictionary
        Dictionary<int, string> shardAssignments = new();
        foreach (PeerNode peer in _peers.Values)
        {
            foreach (int shardId in peer.GetShardsSnapshot())
            {
                shardAssignments[shardId] = peer.ProcessId;
            }
        }

        int healthyShards = _peers.Values.Where(p => p.IsHealthy).SelectMany(p => p.GetShardsSnapshot()).Distinct().Count();
        int totalGuilds = _peers.Values.Where(p => p.LatestMetrics != null).SelectMany(p => p.LatestMetrics!.Shards).Sum(s => s.GuildCount);

        ClusterState clusterState = new(
            TotalShards: _totalShards,
            HealthyShards: healthyShards,
            TotalGuilds: totalGuilds,
            Nodes: _peers.Values.Select(p => p.ToState()).ToList(),
            CoordinatorId: _coordinatorId,
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        );

        CoordinatorHandoffData handoff = new(
            SuccessionOrder: _succession.GetAll(),
            PeerNodes: _peers.Values.Select(p => p.ToState()).ToList(),
            ShardAssignments: shardAssignments,
            TotalShards: _totalShards,
            ClusterState: clusterState,
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        );

        await _server.RespondAsync(context, 200, handoff);

        // Stop coordinating (original will take over)
        _logger.Log(LogLevel.Information, "Handing off coordinator role to original coordinator");
        await StopAsync().ConfigureAwait(false);
    }

    private async Task HandleHandoffAsync(System.Net.HttpListenerContext context)
    {
        if (!_isOriginalCoordinator)
        {
            await _server.RespondAsync(context, 400, new HttpErrorResponse { error = "Not original coordinator" });
            return;
        }

        CoordinatorHandoffData? handoff = await _server.ReadJsonAsync<CoordinatorHandoffData>(context);
        if (handoff == null)
        {
            await _server.RespondAsync(context, 400, new HttpErrorResponse { error = "Invalid handoff data" });
            return;
        }

        _logger.Log(LogLevel.Information, "Receiving coordinator state from temporary coordinator");

        // Restore state
        _totalShards = handoff.TotalShards;
        _succession.LoadFrom(handoff.SuccessionOrder);
        _peers.Clear();
        foreach (PeerNodeState peerState in handoff.PeerNodes)
        {
            PeerNode peer = PeerNode.FromState(peerState);
            _peers.TryAdd(peer.ProcessId, peer);
        }

        await _server.RespondAsync(context, 200, new SuccessResponse { success = true });

        // Announce resumption to all workers
        await AnnounceResumptionAsync();

        _logger.Log(LogLevel.Information, "Coordinator role resumed successfully");
    }

    private async Task AnnounceResumptionAsync()
    {
        CoordinatorResumedAnnouncement announcement = new(
            ResumedCoordinatorId: _coordinatorId,
            ResumedCoordinatorUrl: _listenUrl,
            PreviousCoordinatorId: "temp-coordinator",
            SuccessionOrder: _succession.GetAll(),
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Message: "Original coordinator has resumed"
        );

        PeerNode[] workers = _peers.Values.ToArray();
        foreach (PeerNode worker in workers)
        {
            try
            {
                PeerNodeState state = worker.ToState();
                await _client.PostAsync($"{state.Url}/coordinator/resumed", announcement);
            }
            catch (Exception ex)
            {
                var state = worker.ToState();
                _logger.Log(LogLevel.Error, $"Failed to announce resumption to {state.ProcessId}: {ex.Message}", ex);
            }
        }
    }

    private async Task BroadcastSuccessionUpdateAsync()
    {
        SuccessionUpdate update = _succession.CreateUpdate();
        PeerNode[] workers = _peers.Values.ToArray();

        foreach (PeerNode worker in workers)
        {
            try
            {
                PeerNodeState state = worker.ToState();
                await _client.PostAsync($"{state.Url}/succession", update);
            }
            catch (Exception ex)
            {
                PeerNodeState state = worker.ToState();
                _logger.Log(LogLevel.Error, $"Failed to send succession update to {state.ProcessId}: {ex.Message}", ex);
            }
        }
    }

    private void RequestMetrics(object? state)
    {
        // Metrics are sent via POST /metrics by workers every 5s
    }

    private void RunLoadBalancing(object? state)
    {
        try
        {
            _loadBalancer.AnalyzeAndBalance();
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Load balancing error: {ex.Message}", ex);
        }
    }

    private void OnPeerFailed(PeerNode peer)
    {
        PeerNodeState state = peer.ToState();
        _logger.Log(LogLevel.Error, $"Peer {state.ProcessId} failed, removing from cluster");

        if (!_peers.TryRemove(peer.ProcessId, out _)) return;
        _succession.RemoveWorker(peer.ProcessId);

        Dictionary<PeerNode, List<int>> reassigned;
        lock (_assignmentLock)
        {
            // Reassign orphaned shards. Any left over stay unassigned until the next worker registers.
            reassigned = ShardAssigner.ReassignShards(peer.GetShardsSnapshot(), _peers.Values.Where(p => p.IsHealthy).ToArray());
        }

        foreach ((PeerNode target, List<int> shards) in reassigned)
        {
            _logger.Log(LogLevel.Information, $"Reassigned orphaned shards [{string.Join(", ", shards)}] to {target.ProcessId}");
            _ = SendAssignmentAsync(target, shards, $"Failover from {peer.ProcessId}");
        }
    }

    private async Task SendAssignmentAsync(PeerNode target, List<int> shards, string reason)
    {
        try
        {
            ShardAssignment assignment = new(
                Shards: shards,
                Reason: reason,
                Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            );
            await _client.PostAsync($"{target.Url}/assignment", assignment).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Failed to send shard assignment to {target.ProcessId}: {ex.Message}", ex);
        }
    }

    private void OnMigrationNeeded(ShardMigrationRequest migration)
    {
        if (!_peers.TryGetValue(migration.FromNode, out PeerNode? from) ||
            !_peers.TryGetValue(migration.ToNode, out PeerNode? to)) return;

        lock (_assignmentLock)
        {
            lock (from.ShardsLock)
                from.AssignedShards.Remove(migration.ShardId);
            lock (to.ShardsLock)
                to.AssignedShards.Add(migration.ShardId);
        }

        // Send migration command to both workers
        _ = Task.Run(async () =>
        {
            try
            {
                PeerNodeState fromState = from.ToState();
                PeerNodeState toState = to.ToState();

                await _client.PostAsync($"{fromState.Url}/migrate", migration);

                ShardAssignment assignment = new ShardAssignment(
                    Shards: [migration.ShardId],
                    Reason: $"Migration from {migration.FromNode}",
                    Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                );

                await _client.PostAsync($"{toState.Url}/assignment", assignment);
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, $"Migration execution error: {ex.Message}", ex);
            }
        });
    }

    /// <summary>
    /// Stops the coordinator and shuts down all services.
    /// Example: await coordinator.StopAsync();
    /// </summary>
    public async Task StopAsync()
    {
        if (_disposed) return;

        try
        {
            try { _metricsTimer.Change(Timeout.Infinite, Timeout.Infinite); } catch (ObjectDisposedException) { }
            try { _balancingTimer.Change(Timeout.Infinite, Timeout.Infinite); } catch (ObjectDisposedException) { }

            using var metricsDone = new ManualResetEvent(false);
            _metricsTimer.Dispose(metricsDone);
            metricsDone.WaitOne();

            using var balancingDone = new ManualResetEvent(false);
            _balancingTimer.Dispose(balancingDone);
            balancingDone.WaitOne();

            _timersDisposed = true;

            await Task.CompletedTask;
        }
        catch
        {
            // Swallow exceptions during shutdown
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (!_timersDisposed)
        {
            _metricsTimer.Dispose();
            _balancingTimer.Dispose();
        }
        _healthMonitor.Dispose();
        _server.Dispose();
        _client.Dispose();

        _logger.Log(LogLevel.Information, "Coordinator disposed");
    }
}
