using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using SimpleDiscordNet.Logging;
using SimpleDiscordNet.Sharding.Models;

namespace SimpleDiscordNet.Sharding;

/// <summary>
/// Worker node that connects to a coordinator, receives shard assignments, and reports metrics.
/// Handles shard lifecycle, coordinator failover, and automatic re-registration.
/// Example: var worker = new DistributedWorker(token, intents, json, processId, workerUrl, coordinatorUrl, logger);
/// </summary>
internal sealed class DistributedWorker : IDisposable
{
    private readonly string _token;
    private readonly DiscordIntents _intents;
    private readonly JsonSerializerOptions _json;
    private readonly string _processId;
    private readonly string _workerUrl;
    private readonly NativeLogger _logger;
    private readonly ShardHttpClient _client;
    private readonly ShardHttpServer _server;
    private readonly ShardManager _shardManager;
    private readonly Timer _metricsTimer;
    private readonly ConcurrentDictionary<string, PeerNode> _knownPeers = new();
    private readonly SuccessionManager _succession;
    private volatile string _coordinatorUrl;
    private volatile bool _disposed;
    private volatile int _totalShards;

    public DistributedWorker(
        string token,
        DiscordIntents intents,
        JsonSerializerOptions json,
        NativeLogger logger,
        string processId,
        string workerUrl,
        string coordinatorUrl)
    {
        _token = token;
        _intents = intents;
        _json = json;
        _logger = logger;
        _processId = processId;
        _workerUrl = workerUrl;
        _coordinatorUrl = coordinatorUrl;
        _client = new ShardHttpClient();
        _server = new ShardHttpServer(workerUrl);
        _shardManager = new ShardManager(token, intents, json, logger);
        _succession = new SuccessionManager(logger);
        _metricsTimer = new Timer(SendMetrics, null, Timeout.Infinite, Timeout.Infinite);

        SetupEndpoints();
    }

    private void SetupEndpoints()
    {
        _server.OnAssignment(HandleAssignmentAsync);
        _server.OnMigration(HandleMigrationAsync);
        _server.OnSuccession(HandleSuccessionAsync);
        _server.OnResumedAnnouncement(HandleResumedAnnouncementAsync);
        _server.OnHealth(HandleHealthAsync);
    }

    /// <summary>
    /// Starts the worker, registers with coordinator, and begins metrics reporting.
    /// Example: await worker.StartAsync(ct);
    /// </summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(DistributedWorker));

        _server.Start();

        // Register with coordinator
        await RegisterWithCoordinatorAsync(ct).ConfigureAwait(false);

        // Start metrics reporting every 5 seconds
        _metricsTimer.Change(5000, 5000);

        _logger.Log(LogLevel.Information, $"DistributedWorker {_processId} started successfully");
    }

    private async Task RegisterWithCoordinatorAsync(CancellationToken ct)
    {
        try
        {
            WorkerCapabilities capabilities = new(
                MemoryMb: GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024),
                CpuCores: Environment.ProcessorCount,
                Platform: Environment.OSVersion.Platform.ToString()
            );

            WorkerRegistrationRequest request = new(
                ProcessId: _processId,
                ListenUrl: _workerUrl,
                MaxShards: null,
                Capabilities: capabilities,
                Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            );

            WorkerRegistrationResponse? response = await _client.PostAsync<WorkerRegistrationRequest, WorkerRegistrationResponse>(
                $"{_coordinatorUrl}/register",
                request,
                ct
            ).ConfigureAwait(false);

            if (response == null)
            {
                throw new InvalidOperationException("Registration failed: No response from coordinator");
            }

            _totalShards = response.TotalShards;
            _succession.LoadFrom(response.SuccessionOrder);

            _logger.Log(LogLevel.Information, $"Registered with coordinator: {_totalShards} total shards, assigned shards: [{string.Join(", ", response.AssignedShards)}]");

            // Start assigned shards
            foreach (int shardId in response.AssignedShards)
            {
                await _shardManager.StartShardAsync(shardId, _totalShards, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Failed to register with coordinator: {ex.Message}", ex);
            throw;
        }
    }

    private async Task HandleAssignmentAsync(System.Net.HttpListenerContext context)
    {
        ShardAssignment? assignment = await _server.ReadJsonAsync<ShardAssignment>(context);
        if (assignment == null)
        {
            await _server.RespondAsync(context, 400, new { error = "Invalid assignment" });
            return;
        }

        try
        {
            // ShardAssignment has Shards: List<int>
            foreach (int shardId in assignment.Shards)
            {
                await _shardManager.StartShardAsync(shardId, _totalShards, CancellationToken.None).ConfigureAwait(false);
                _logger.Log(LogLevel.Information, $"Started new shard assignment: {shardId}/{_totalShards} - Reason: {assignment.Reason}");
            }

            await _server.RespondAsync(context, 200, new { success = true });
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Failed to start assigned shards: {ex.Message}", ex);
            await _server.RespondAsync(context, 500, new { error = ex.Message });
        }
    }

    private async Task HandleMigrationAsync(System.Net.HttpListenerContext context)
    {
        ShardMigrationRequest? migration = await _server.ReadJsonAsync<ShardMigrationRequest>(context);
        if (migration == null)
        {
            await _server.RespondAsync(context, 400, new { error = "Invalid migration" });
            return;
        }

        try
        {
            // This worker is the source, stop the shard
            if (migration.FromNode == _processId)
            {
                await _shardManager.StopShardAsync(migration.ShardId).ConfigureAwait(false);
                _logger.Log(LogLevel.Information, $"Migrated shard {migration.ShardId} to {migration.ToNode}");
            }
            // This worker is the target, start the shard (handled via /assignment)

            await _server.RespondAsync(context, 200, new { success = true });
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Failed to handle migration: {ex.Message}", ex);
            await _server.RespondAsync(context, 500, new { error = ex.Message });
        }
    }

    private async Task HandleSuccessionAsync(System.Net.HttpListenerContext context)
    {
        SuccessionUpdate? update = await _server.ReadJsonAsync<SuccessionUpdate>(context);
        if (update == null)
        {
            await _server.RespondAsync(context, 400, new { error = "Invalid succession update" });
            return;
        }

        _succession.LoadFrom(update.Succession);
        int myPosition = _succession.GetPosition(_processId);
        _logger.Log(LogLevel.Information, $"Succession updated: I am at position {myPosition} (1 = coordinator, 2 = next)");

        await ShardHttpServer.RespondAsync(context, 204);
    }

    private async Task HandleResumedAnnouncementAsync(System.Net.HttpListenerContext context)
    {
        CoordinatorResumedAnnouncement? announcement = await _server.ReadJsonAsync<CoordinatorResumedAnnouncement>(context);
        if (announcement == null)
        {
            await _server.RespondAsync(context, 400, new { error = "Invalid announcement" });
            return;
        }

        _logger.Log(LogLevel.Information, $"Original coordinator resumed at {announcement.ResumedCoordinatorUrl}");

        // Update coordinator URL
        _coordinatorUrl = announcement.ResumedCoordinatorUrl;

        // Re-register with original coordinator
        await _server.RespondAsync(context, 200, new { success = true });

        _ = Task.Run(async () =>
        {
            await Task.Delay(1000); // Small delay to let coordinator settle
            try
            {
                await RegisterWithCoordinatorAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, $"Failed to re-register with resumed coordinator: {ex.Message}", ex);
            }
        });
    }

    private async Task HandleHealthAsync(System.Net.HttpListenerContext context)
    {
        HealthCheckResponse response = new HealthCheckResponse(
            Status: "healthy",
            Shards: _shardManager.GetShardIds().ToList(),
            IsCoordinator: false,
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        );

        await _server.RespondAsync(context, 200, response);
    }

    private void SendMetrics(object? state)
    {
        if (_disposed) return;

        try
        {
            ShardInfo[] shardInfos = _shardManager.GetAllShardInfos();
            List<ShardMetrics> shardMetrics = shardInfos.Select(s => new ShardMetrics(
                Id: s.Id,
                Status: s.Status.ToString(),
                GuildCount: s.GuildCount,
                GatewayLatency: s.Latency,
                EventsPerSecond: s.EventsPerSecond,
                CommandsPerSecond: s.CommandsPerSecond
            )).ToList();

            WorkerMetrics metrics = new(
                ProcessId: _processId,
                Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                CpuUsage: GetCpuUsage(),
                MemoryUsageMb: GetMemoryUsage() / (1024 * 1024),
                Shards: shardMetrics,
                Health: "healthy"
            );

            _ = Task.Run(async () =>
            {
                try
                {
                    await _client.PostAsync($"{_coordinatorUrl}/metrics", metrics, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Error, $"Failed to send metrics: {ex.Message}", ex);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Error collecting metrics: {ex.Message}", ex);
        }
    }

    private static double GetCpuUsage()
    {
        try
        {
            Process process = Process.GetCurrentProcess();
            DateTime startTime = DateTime.UtcNow;
            TimeSpan startCpuTime = process.TotalProcessorTime;

            Thread.Sleep(100);

            DateTime endTime = DateTime.UtcNow;
            TimeSpan endCpuTime = process.TotalProcessorTime;

            double cpuUsedMs = (endCpuTime - startCpuTime).TotalMilliseconds;
            double totalPassedMs = (endTime - startTime).TotalMilliseconds;

            double cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalPassedMs);
            return Math.Max(0, Math.Min(1, cpuUsageTotal));
        }
        catch
        {
            return 0;
        }
    }

    private static long GetMemoryUsage()
    {
        try
        {
            return Process.GetCurrentProcess().WorkingSet64;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Gets the shard manager for accessing local shards.
    /// Example: var shardManager = worker.ShardManager;
    /// </summary>
    public ShardManager ShardManager => _shardManager;

    /// <summary>
    /// Gets the worker ID (process ID).
    /// Example: string id = worker.ProcessId;
    /// </summary>
    public string ProcessId => _processId;

    /// <summary>
    /// Detects if this worker should become the coordinator (if current coordinator failed).
    /// Example: if (worker.ShouldBecomeCoordinator()) { ... }
    /// </summary>
    public bool ShouldBecomeCoordinator()
    {
        return _succession.GetPosition(_processId) == 1; // Position 1 = coordinator
    }

    /// <summary>
    /// Stops the worker, unregisters from coordinator, and shuts down all shards.
    /// Example: await worker.StopAsync();
    /// </summary>
    public async Task StopAsync()
    {
        if (_disposed) return;

        try
        {
            // Stop metrics reporting
            _metricsTimer.Change(Timeout.Infinite, Timeout.Infinite);

            // Stop all shards
            foreach (int shardId in _shardManager.GetShardIds())
            {
                await _shardManager.StopShardAsync(shardId).ConfigureAwait(false);
            }

            // Stop HTTP server (Dispose stops the listener)
            // No explicit Stop method, handled in Dispose
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
        _shardManager.Dispose();
        _server.Dispose();
        _client.Dispose();

        _logger.Log(LogLevel.Information, $"DistributedWorker {_processId} disposed");
    }
}
