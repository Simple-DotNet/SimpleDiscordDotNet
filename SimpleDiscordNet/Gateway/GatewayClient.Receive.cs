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
                    ClientWebSocket ws = _ws;
                    if (ws.State != WebSocketState.Open)
                    {
                    if (!_autoReconnect) return;
                    Disconnected?.Invoke(this, null);
                    bool reconnected = await SafeReconnectAsync(ct).ConfigureAwait(false);
                        if (!reconnected)
                        {
                            await _reconnectGate.WaitAsync(ct).ConfigureAwait(false);
                            try
                            {
                                ws = _ws;
                                if (!_autoReconnect || ws.State != WebSocketState.Open) return;
                            }
                            finally { _reconnectGate.Release(); }
                            goto ContinueLoop;
                        }
                        ws = _ws;
                        if (ws.State != WebSocketState.Open) return;
                    }
                    memoryStream.SetLength(0);
                    WebSocketReceiveResult? result;
                    do
                    {
                        result = await ws.ReceiveAsync(seg, ct).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            int closeCode = (int)(result.CloseStatus ?? WebSocketCloseStatus.Empty);
                            string reason = $"Close: {result.CloseStatus} - {result.CloseStatusDescription}";
                            Error?.Invoke(this, new WebSocketException(closeCode, reason));
                            Disconnected?.Invoke(this, new WebSocketException(closeCode, reason));

                            if (closeCode is 4004 or 4010 or 4011 or 4012)
                            {
                                _autoReconnect = false;
                                try { await DisconnectAsync().ConfigureAwait(false); } catch { }
                                return;
                            }

                            if (closeCode == 4003)
                            {
                                if (_sessionExpired)
                                {
                                    _sessionId = null;
                                    Interlocked.Exchange(ref _seq, 0);
                                    SessionReset?.Invoke(this, EventArgs.Empty);
                                }
                                else
                                {
                                    _sessionExpired = true;
                                }
                            }

                            if (!_autoReconnect)
                            {
                                await DisconnectAsync().ConfigureAwait(false);
                                return;
                            }
                            try { await Task.Delay(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false); }
                            catch (OperationCanceledException) { break; }

                            bool reconnected = await SafeReconnectAsync(ct).ConfigureAwait(false);
                            if (!reconnected)
                            {
                                await _reconnectGate.WaitAsync(ct).ConfigureAwait(false);
                                try
                                {
                                    ClientWebSocket ws2 = _ws;
                                    if (!_autoReconnect || ws2.State != WebSocketState.Open) return;
                                }
                                finally { _reconnectGate.Release(); }
                                goto ContinueLoop;
                            }
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
                            Disconnected?.Invoke(this, null);
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
                            Interlocked.Exchange(ref _isReady, 0);
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
                        Disconnected?.Invoke(this, ex);
                        bool reconnected = await SafeReconnectAsync(ct).ConfigureAwait(false);
                        if (!reconnected)
                        {
                            await _reconnectGate.WaitAsync(ct).ConfigureAwait(false);
                            try
                            {
                                ClientWebSocket ws = _ws;
                                if (!_autoReconnect || ws.State != WebSocketState.Open) return;
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
