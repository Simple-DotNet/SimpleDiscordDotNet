using System.Net.WebSockets;
using System.Text.Json;

namespace SimpleDiscordNet.Gateway;

internal sealed partial class GatewayClient
{
    private async Task ReceiveLoop(CancellationToken ct)
    {
        byte[] buffer = new byte[65536];
        using var memoryStream = new MemoryStream(65536);
        ArraySegment<byte> seg = buffer;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    if (_ws.State != WebSocketState.Open)
                    {
                        if (!_autoReconnect) return;
                        bool reconnected = await SafeReconnectAsync(ct).ConfigureAwait(false);
                        if (!reconnected)
                        {
                            await _reconnectGate.WaitAsync(ct).ConfigureAwait(false);
                            try
                            {
                                if (!_autoReconnect || _ws.State != WebSocketState.Open) return;
                            }
                            finally { _reconnectGate.Release(); }
                            goto ContinueLoop;
                        }
                        if (_ws.State != WebSocketState.Open) return;
                    }
                    memoryStream.SetLength(0);
                    WebSocketReceiveResult? result;
                    do
                    {
                        result = await _ws.ReceiveAsync(seg, ct).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            // Attempt to reconnect, according to gateway policy
                            if (!_autoReconnect)
                            {
                                await DisconnectAsync().ConfigureAwait(false);
                                return;
                            }
                            bool reconnected = await SafeReconnectAsync(ct).ConfigureAwait(false);
                            if (!reconnected)
                            {
                                await _reconnectGate.WaitAsync(ct).ConfigureAwait(false);
                                try
                                {
                                    if (!_autoReconnect || _ws.State != WebSocketState.Open) return;
                                }
                                finally { _reconnectGate.Release(); }
                                goto ContinueLoop;
                            }
                            // continue to next iteration with new socket
                            goto ContinueLoop;
                        }
                        memoryStream.Write(buffer.AsSpan(0, result.Count));
                    } while (!result.EndOfMessage);

                    ReadOnlySpan<byte> jsonBytes = memoryStream.GetBuffer().AsSpan(0, (int)memoryStream.Length);
                    GatewayPayload? payload = JsonSerializer.Deserialize<GatewayPayload>(jsonBytes, json);
                    if (payload == null) continue;
                    if (payload.s.HasValue) Interlocked.Exchange(ref _seq, payload.s.Value);

                    switch (payload.op)
                    {
                        case 10: // Hello
                            int interval = payload.d.GetProperty("heartbeat_interval").GetInt32();
                            _heartbeatIntervalMs = interval;
                            StartHeartbeat();
                            if (!string.IsNullOrEmpty(_sessionId) && Interlocked.Read(ref _seq) > 0)
                            {
                                await ResumeAsync(ct).ConfigureAwait(false);
                            }
                            else
                            {
                                await IdentifyAsync(ct).ConfigureAwait(false);
                            }
                            break;
                        case 11: // Heartbeat ACK
                            _awaitingHeartbeatAck = false;
                            Interlocked.Exchange(ref _missedHeartbeatAcks, 0);
                            break;
                        case 0: // Dispatch
                            HandleDispatch(payload.t, payload.d);
                            break;
                        case 7: // RECONNECT
                            if (_autoReconnect)
                            {
                                bool reconnected = await SafeReconnectAsync(ct).ConfigureAwait(false);
                                if (!reconnected) goto ContinueLoop;
                            }
                            break;
                        case 9: // INVALID_SESSION
                            bool canResume = false;
                            try { canResume = payload.d.ValueKind == JsonValueKind.True || (payload.d.ValueKind != JsonValueKind.False && payload.d.GetBoolean()); }
                            catch { /* Invalid session data format, default to false */ }
                            // Random jitter 1-5s as per Discord suggestion
                            int delay = _rand.Next(1000, 5000);
                            await Task.Delay(delay, ct).ConfigureAwait(false);
                            if (canResume && !string.IsNullOrEmpty(_sessionId) && Interlocked.Read(ref _seq) > 0)
                            {
                                await ResumeAsync(ct).ConfigureAwait(false);
                            }
                            else
                            {
                                _sessionId = null; Interlocked.Exchange(ref _seq, 0);
                                await IdentifyAsync(ct).ConfigureAwait(false);
                            }
                            break;
                    }
                ContinueLoop: ;
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Error?.Invoke(this, ex);
                if (_autoReconnect && !ct.IsCancellationRequested)
                {
                    try
                    {
                        bool reconnected = await SafeReconnectAsync(ct).ConfigureAwait(false);
                        if (!reconnected)
                        {
                            await _reconnectGate.WaitAsync(ct).ConfigureAwait(false);
                            try
                            {
                                if (!_autoReconnect || _ws.State != WebSocketState.Open) return;
                            }
                            finally { _reconnectGate.Release(); }
                        }
                    }
                    catch { /* Reconnection failed, will retry on next loop */ }
                    if (!ct.IsCancellationRequested)
                    {
                        try { await Task.Delay(GetBackoffDelayMs(), ct).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
                    }
                    continue;
                }
            }
        }
    }
}
