using System.Collections.Concurrent;
using SimpleDiscordNet.Logging;
using SimpleDiscordNet.Sharding.Models;

namespace SimpleDiscordNet.Sharding;

/// <summary>
/// Monitors health of all peer nodes via periodic heartbeat checks.
/// Detects failures after 3 missed heartbeats (15 seconds) and triggers removal.
/// Example: var monitor = new HealthMonitor(peers, OnPeerFailed, logger); monitor.Start();
/// </summary>
internal sealed class HealthMonitor : IDisposable
{
    private readonly ConcurrentDictionary<string, PeerNode> _peers;
    private readonly Action<PeerNode> _onPeerFailed;
    private readonly NativeLogger _logger;
    private readonly Timer _timer;
    private volatile bool _disposed;

    private const int CheckIntervalMs = 5000; // Check every 5 seconds
    private const int FailureThresholdMs = 15000; // 3 missed heartbeats (15 seconds)

    public HealthMonitor(ConcurrentDictionary<string, PeerNode> peers, Action<PeerNode> onPeerFailed, NativeLogger logger)
    {
        _peers = peers;
        _onPeerFailed = onPeerFailed;
        _logger = logger;
        _timer = new Timer(CheckHealth, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Starts the health monitor.
    /// Example: monitor.Start();
    /// </summary>
    public void Start()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(HealthMonitor));
        _timer.Change(CheckIntervalMs, CheckIntervalMs);
        _logger.Log(LogLevel.Information, "HealthMonitor started");
    }

    /// <summary>
    /// Stops the health monitor.
    /// Example: monitor.Stop();
    /// </summary>
    public void Stop()
    {
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        _logger.Log(LogLevel.Information, "HealthMonitor stopped");
    }

    private void CheckHealth(object? state)
    {
        if (_disposed) return;

        try
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            List<PeerNode> failedPeers = [];

            foreach (PeerNode peer in _peers.Values)
            {
                PeerNodeState peerState = peer.ToState();
                long timeSinceHeartbeat = now - peerState.LastHeartbeat;

                if (timeSinceHeartbeat > FailureThresholdMs)
                {
                    failedPeers.Add(peer);
                }
            }

            foreach (PeerNode failed in failedPeers)
            {
                PeerNodeState failedState = failed.ToState();
                double secondsSinceHeartbeat = (now - failedState.LastHeartbeat) / 1000.0;
                _logger.Log(LogLevel.Error, $"Peer {failedState.ProcessId} failed health check (last heartbeat: {secondsSinceHeartbeat:F1}s ago)");
                _onPeerFailed(failed);
            }
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"HealthMonitor check error: {ex.Message}", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Dispose();
    }
}
