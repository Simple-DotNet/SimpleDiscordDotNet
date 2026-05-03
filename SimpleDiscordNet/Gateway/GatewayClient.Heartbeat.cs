using System.Buffers;
using System.Net.WebSockets;
using System.Text.Json;

namespace SimpleDiscordNet.Gateway;

internal sealed partial class GatewayClient
{
    private void StartHeartbeat()
    {
        lock (_heartbeatLock)
        {
            if (_ctsHeartbeat != null)
            {
                try { _ctsHeartbeat.Cancel(); } catch { /* ignored */ }
                try { _ctsHeartbeat.Dispose(); } catch { /* ignored */ }
            }
            _ctsHeartbeat = new CancellationTokenSource();
            _ = HeartbeatLoopAsync(_ctsHeartbeat.Token);
        }
    }

    private async Task HeartbeatLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_heartbeatIntervalMs, ct).ConfigureAwait(false);
                await SendHeartbeatAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Error?.Invoke(this, ex);
                try { await Task.Delay(1000, ct).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task SendHeartbeatAsync(CancellationToken ct)
    {
        if (_ws.State != WebSocketState.Open) return;
        if (_awaitingHeartbeatAck)
        {
            int missed = Interlocked.Increment(ref _missedHeartbeatAcks);
            if (missed >= 2 && _autoReconnect)
            {
                await SafeReconnectAsync(ct).ConfigureAwait(false);
                return;
            }
        }
        Heartbeat hb = new() { d = Interlocked.Read(ref _seq) };
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            JsonSerializer.Serialize(writer, hb, json);
        }
        await _ws.SendAsync(buffer.WrittenMemory, WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
        _awaitingHeartbeatAck = true;
    }
}
