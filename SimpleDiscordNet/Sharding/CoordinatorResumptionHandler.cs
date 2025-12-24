using SimpleDiscordNet.Logging;
using SimpleDiscordNet.Sharding.Models;

namespace SimpleDiscordNet.Sharding;

/// <summary>
/// Handles the coordinator resumption protocol when the original coordinator recovers.
/// Manages state transfer from temporary coordinator and worker re-registration.
/// Example: var handler = new CoordinatorResumptionHandler(client, myId, originalUrl, logger);
/// </summary>
internal sealed class CoordinatorResumptionHandler
{
    private readonly ShardHttpClient _client;
    private readonly string _processId;
    private readonly string _originalCoordinatorUrl;
    private readonly NativeLogger _logger;

    public CoordinatorResumptionHandler(ShardHttpClient client, string processId, string originalCoordinatorUrl, NativeLogger logger)
    {
        _client = client;
        _processId = processId;
        _originalCoordinatorUrl = originalCoordinatorUrl;
        _logger = logger;
    }

    /// <summary>
    /// Requests resumption from the temporary coordinator, receiving cluster state handoff.
    /// Example: var handoff = await handler.RequestResumptionAsync(tempCoordinatorUrl, ct);
    /// </summary>
    public async Task<CoordinatorHandoffData?> RequestResumptionAsync(string temporaryCoordinatorUrl, CancellationToken ct = default)
    {
        try
        {
            CoordinatorResumptionRequest request = new(
                OriginalCoordinatorId: _processId,
                OriginalCoordinatorUrl: _originalCoordinatorUrl,
                Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            );

            CoordinatorHandoffData? handoff = await _client.PostAsync<CoordinatorResumptionRequest, CoordinatorHandoffData>(
                $"{temporaryCoordinatorUrl}/coordinator/resume",
                request,
                ct
            ).ConfigureAwait(false);

            if (handoff != null)
            {
                _logger.Log(LogLevel.Information, $"Received coordinator handoff: {handoff.TotalShards} shards, {handoff.PeerNodes.Count} peers");
            }

            return handoff;
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Failed to request coordinator resumption: {ex.Message}", ex);
            return null;
        }
    }

    /// <summary>
    /// Loads handoff data into a coordinator instance.
    /// Example: handler.LoadHandoffData(coordinator, handoffData);
    /// </summary>
    public static void LoadHandoffData(ShardCoordinator coordinator, CoordinatorHandoffData handoff)
    {
        // This is handled directly in ShardCoordinator.HandleHandoffAsync
        // This method exists for future extensibility
    }

    /// <summary>
    /// Announces coordinator resumption to all workers.
    /// Example: await handler.AnnounceResumptionAsync(workers, successionOrder, ct);
    /// </summary>
    public async Task AnnounceResumptionAsync(PeerNode[] workers, List<SuccessionEntry> successionOrder, string previousCoordinatorId, CancellationToken ct = default)
    {
        CoordinatorResumedAnnouncement announcement = new(
            ResumedCoordinatorId: _processId,
            ResumedCoordinatorUrl: _originalCoordinatorUrl,
            PreviousCoordinatorId: previousCoordinatorId,
            SuccessionOrder: successionOrder,
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Message: "Original coordinator has resumed control"
        );

        IEnumerable<Task> tasks = workers.Select(async worker =>
        {
            try
            {
                PeerNodeState state = worker.ToState();
                await _client.PostAsync($"{state.Url}/coordinator/resumed", announcement, ct).ConfigureAwait(false);
                _logger.Log(LogLevel.Information, $"Announced resumption to worker {state.ProcessId}");
            }
            catch (Exception ex)
            {
                PeerNodeState state = worker.ToState();
                _logger.Log(LogLevel.Error, $"Failed to announce resumption to {state.ProcessId}: {ex.Message}", ex);
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies coordinator health by checking the health endpoint.
    /// Example: bool healthy = await handler.VerifyCoordinatorHealthAsync(url, ct);
    /// </summary>
    public async Task<bool> VerifyCoordinatorHealthAsync(string coordinatorUrl, CancellationToken ct = default)
    {
        try
        {
            HealthCheckResponse? response = await _client.GetAsync<HealthCheckResponse>($"{coordinatorUrl}/health", ct).ConfigureAwait(false);
            return response is { Status: "healthy" };
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Waits for the original coordinator to become available again.
    /// Example: bool recovered = await handler.WaitForCoordinatorRecoveryAsync(ct);
    /// </summary>
    public async Task<bool> WaitForCoordinatorRecoveryAsync(CancellationToken ct = default, int maxAttempts = 60)
    {
        for (int attempt = 0; attempt < maxAttempts && !ct.IsCancellationRequested; attempt++)
        {
            if (await VerifyCoordinatorHealthAsync(_originalCoordinatorUrl, ct).ConfigureAwait(false))
            {
                _logger.Log(LogLevel.Information, $"Original coordinator at {_originalCoordinatorUrl} has recovered");
                return true;
            }

            await Task.Delay(5000, ct).ConfigureAwait(false); // Check every 5s
        }

        return false;
    }
}
