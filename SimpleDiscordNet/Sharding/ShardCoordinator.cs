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
    private volatile int _totalShards;
    private volatile bool _disposed;
    private readonly string _coordinatorId;

    public ShardCoordinator(string token, string listenUrl, NativeLogger logger, bool isOriginalCoordinator = true)
    {
        _token = token;
        _listenUrl = listenUrl;
        _logger = logger;
        _isOriginalCoordinator = isOriginalCoordinator;
        _coordinatorId = $"coordinator-{Guid.NewGuid():N}";
        _server = new ShardHttpServer(listenUrl);
        _client = new ShardHttpClient();
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
        _totalShards = await AutoDetectShardCountAsync(ct).ConfigureAwait(false);
        _logger.Log(LogLevel.Information, $"Coordinator detected {_totalShards} total shards from Discord");

        _server.Start();
        _healthMonitor.Start();
        _metricsTimer.Change(5000, 5000); // Request metrics every 5s
        _balancingTimer.Change(10000, 10000); // Load balancing every 10s

        _logger.Log(LogLevel.Information, $"Coordinator started on {_listenUrl} (Original: {_isOriginalCoordinator})");
    }

    private async Task<int> AutoDetectShardCountAsync(CancellationToken ct)
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("Authorization", $"Bot {_token}");

            var response = await http.GetAsync("https://discord.com/api/v10/gateway/bot", ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var shards = doc.RootElement.GetProperty("shards").GetInt32();

            return shards;
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Failed to auto-detect shard count: {ex.Message}. Defaulting to 1.", ex);
            return 1;
        }
    }

    private async Task HandleRegisterAsync(System.Net.HttpListenerContext context)
    {
        var request = await _server.ReadJsonAsync<WorkerRegistrationRequest>(context);
        if (request == null || string.IsNullOrEmpty(request.ProcessId))
        {
            await _server.RespondAsync(context, 400, new { error = "Invalid request" });
            return;
        }

        PeerNode peer = new(request.ProcessId, request.ListenUrl)
        {
            MaxShards = request.MaxShards
        };
        _peers.AddOrUpdate(request.ProcessId, peer, (_, old) => peer);

        int position = _succession.AddWorker(request.ProcessId, request.ListenUrl, false);

        // Auto-assign shards
        AssignShardsToWorker(peer);

        WorkerRegistrationResponse response = new(
            AssignedShards: peer.AssignedShards.ToList(),
            TotalShards: _totalShards,
            SuccessionPosition: position,
            SuccessionOrder: _succession.GetAll(),
            PeerUrls: _peers.Values.Select(p => p.ToState().Url).ToList(),
            CoordinatorId: _coordinatorId,
            IsOriginalCoordinator: _isOriginalCoordinator,
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        );

        await _server.RespondAsync(context, 200, response);

        // Broadcast succession update to all peers
        await BroadcastSuccessionUpdateAsync();

        _logger.Log(LogLevel.Information, $"Worker {request.ProcessId} registered with shards: [{string.Join(", ", peer.AssignedShards)}]");
    }

    private void AssignShardsToWorker(PeerNode peer)
    {
        // Find unassigned shards
        HashSet<int> assignedShards = new(_peers.Values.SelectMany(p => p.AssignedShards));
        int[] unassigned = Enumerable.Range(0, _totalShards).Where(s => !assignedShards.Contains(s)).ToArray();

        if (unassigned.Length > 0)
        {
            // Assign next available shard
            peer.AssignedShards.Add(unassigned[0]);
        }
        else
        {
            // All shards assigned, find worker with most shards and take one
            PeerNode? mostLoaded = _peers.Values
                .Where(p => p.ProcessId != peer.ProcessId && p.AssignedShards.Count > 0)
                .OrderByDescending(p => p.AssignedShards.Count)
                .FirstOrDefault();

            if (mostLoaded is not { AssignedShards.Count: > 0 }) return;
            int shardToMove = mostLoaded.AssignedShards[0];
            mostLoaded.AssignedShards.Remove(shardToMove);
            peer.AssignedShards.Add(shardToMove);
        }
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
            await _server.RespondAsync(context, 400, new { error = "Invalid metrics" });
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
            .SelectMany(p => p.AssignedShards)
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
            await _server.RespondAsync(context, 400, new { error = "Already original coordinator" });
            return;
        }

        CoordinatorResumptionRequest? request = await _server.ReadJsonAsync<CoordinatorResumptionRequest>(context);
        if (request == null || string.IsNullOrEmpty(request.OriginalCoordinatorId))
        {
            await _server.RespondAsync(context, 400, new { error = "Invalid request" });
            return;
        }

        _logger.Log(LogLevel.Information, $"Original coordinator {request.OriginalCoordinatorId} requesting resumption");

        // Package handoff data - create shard assignments dictionary
        Dictionary<int, string> shardAssignments = new();
        foreach (PeerNode peer in _peers.Values)
        {
            foreach (int shardId in peer.AssignedShards)
            {
                shardAssignments[shardId] = peer.ProcessId;
            }
        }

        int healthyShards = _peers.Values.Where(p => p.IsHealthy).SelectMany(p => p.AssignedShards).Distinct().Count();
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
    }

    private async Task HandleHandoffAsync(System.Net.HttpListenerContext context)
    {
        if (!_isOriginalCoordinator)
        {
            await _server.RespondAsync(context, 400, new { error = "Not original coordinator" });
            return;
        }

        CoordinatorHandoffData? handoff = await _server.ReadJsonAsync<CoordinatorHandoffData>(context);
        if (handoff == null)
        {
            await _server.RespondAsync(context, 400, new { error = "Invalid handoff data" });
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

        await _server.RespondAsync(context, 200, new { success = true });

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

        // Reassign orphaned shards
        int[] orphanedShards = peer.AssignedShards.ToArray();
        if (orphanedShards.Length <= 0) return;
        PeerNode[] healthyWorkers = _peers.Values.Where(p => p.IsHealthy).ToArray();
        if (healthyWorkers.Length <= 0) return;
        int targetIndex = 0;
        foreach (int shardId in orphanedShards)
        {
            PeerNode target = healthyWorkers[targetIndex % healthyWorkers.Length];
            target.AssignedShards.Add(shardId);
            targetIndex++;

            PeerNodeState targetState = target.ToState();
            _logger.Log(LogLevel.Information, $"Reassigned orphaned shard {shardId} to {targetState.ProcessId}");
        }
    }

    private void OnMigrationNeeded(ShardMigrationRequest migration)
    {
        if (!_peers.TryGetValue(migration.FromNode, out PeerNode? from) ||
            !_peers.TryGetValue(migration.ToNode, out PeerNode? to)) return;
        from.AssignedShards.Remove(migration.ShardId);
        to.AssignedShards.Add(migration.ShardId);

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
            // Stop timers
            _metricsTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _balancingTimer.Change(Timeout.Infinite, Timeout.Infinite);

            // Stop HTTP server (Dispose stops the listener)
            // No explicit Stop method, handled in Dispose

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

        _metricsTimer.Dispose();
        _balancingTimer.Dispose();
        _healthMonitor.Dispose();
        _server.Dispose();
        _client.Dispose();

        _logger.Log(LogLevel.Information, "Coordinator disposed");
    }
}
