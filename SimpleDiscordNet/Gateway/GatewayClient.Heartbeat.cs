using System.Buffers;
using System.Net.WebSockets;
using System.Text.Json;

namespace SimpleDiscordNet.Gateway;

internal sealed partial class GatewayClient
{
    private const int MaxMissedHeartbeatAcks = 2;

    private void StartHeartbeat()
    {
        lock (_heartbeatLock)
        {
            StopHeartbeatLocked();
            _ctsHeartbeat = new CancellationTokenSource();
            _ = HeartbeatLoopAsync(_ctsHeartbeat.Token);
        }
    }

    private void StopHeartbeat()
    {
        lock (_heartbeatLock)
        {
            StopHeartbeatLocked();
        }
    }

    private void StopHeartbeatLocked()
    {
        if (_ctsHeartbeat == null) return;
        try { _ctsHeartbeat.Cancel(); } catch { /* ignored */ }
        try { _ctsHeartbeat.Dispose(); } catch { /* ignored */ }
        _ctsHeartbeat = null;
    }

    private async Task HeartbeatLoopAsync(CancellationToken ct)
    {
        try
        {
            // Discord asks for a random first delay, so reconnecting clients don't all beat together
            await Task.Delay((int)(_heartbeatIntervalMs * Random.Shared.NextDouble()), ct).ConfigureAwait(false);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (_awaitingHeartbeatAck && Interlocked.Increment(ref _missedHeartbeatAcks) >= MaxMissedHeartbeatAcks)
                    {
                        // The connection is dead. Aborting fails the pending receive, and the receive loop reconnects.
                        _ws.Abort();
                        return;
                    }

                    await SendHeartbeatAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Error?.Invoke(this, ex);
                }

                await Task.Delay(_heartbeatIntervalMs, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Heartbeat stopped
        }
    }

    private async Task SendHeartbeatAsync(CancellationToken ct)
    {
        long seq = Interlocked.Read(ref _seq);
        Heartbeat hb = new() { d = seq > 0 ? seq : null };
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            JsonSerializer.Serialize(writer, hb, json);
        }
        LogGatewaySend(1, buffer.WrittenSpan);
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ClientWebSocket ws = _ws;
            if (ws.State != WebSocketState.Open) return;
            await ws.SendAsync(buffer.WrittenMemory, WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
            _awaitingHeartbeatAck = true;
        }
        finally { _writeLock.Release(); }
    }
}
