using System.Net.WebSockets;
using System.Text.Json;

namespace SimpleDiscordNet.Gateway;

internal sealed partial class GatewayClient
{
    // Discord's "do not reconnect" close codes: bad token, invalid shard, sharding required,
    // invalid API version, invalid intents, and disallowed (privileged) intents.
    private static bool IsFatalCloseCode(int closeCode) => closeCode is 4004 or 4010 or 4011 or 4012 or 4013 or 4014;

    // These close codes mean the session can't be resumed, so the next connection identifies.
    private static bool IsSessionEndingCloseCode(int closeCode) => closeCode is 4007 or 4009;

    private static readonly TimeSpan InvalidSessionCloseDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Owns the connection for its whole lifetime. All reconnects happen here, so no other code
    /// path can race it. The heartbeat loop only aborts the socket, which lands in the catch below.
    /// </summary>
    private async Task ReceiveLoop(CancellationToken ct)
    {
        byte[] buffer = new byte[65536];
        using var memoryStream = new MemoryStream(65536);
        ArraySegment<byte> seg = buffer;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                ClientWebSocket ws = _ws;
                if (ws.State != WebSocketState.Open)
                {
                    await ReconnectAsync(null, null, ct).ConfigureAwait(false);
                    continue;
                }

                memoryStream.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(seg, ct).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    memoryStream.Write(buffer.AsSpan(0, result.Count));
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await HandleCloseFrameAsync(result, ct).ConfigureAwait(false);
                    continue;
                }

                ReadOnlySpan<byte> jsonBytes = memoryStream.GetBuffer().AsSpan(0, (int)memoryStream.Length);
                GatewayPayload? payload = JsonSerializer.Deserialize<GatewayPayload>(jsonBytes, json);
                if (payload == null) continue;
                if (payload.s.HasValue) Interlocked.Exchange(ref _seq, payload.s.Value);

                await HandlePayloadAsync(payload, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, ex);
                if (!_autoReconnect) return;

                try
                {
                    await ReconnectAsync(ex, null, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private async Task HandlePayloadAsync(GatewayPayload payload, CancellationToken ct)
    {
        switch (payload.op)
        {
            case 0: // Dispatch
                HandleDispatch(payload.t, payload.d);
                break;

            case 1: // Heartbeat request: Discord wants one now, without waiting for the interval
                await SendHeartbeatAsync(ct).ConfigureAwait(false);
                break;

            case 7: // RECONNECT
                await ReconnectAsync(null, null, ct).ConfigureAwait(false);
                break;

            case 9: // INVALID_SESSION
                bool canResume = payload.d.ValueKind == JsonValueKind.True;
                // Random jitter 1-5s as per Discord suggestion
                await Task.Delay(Random.Shared.Next(1000, 5000), ct).ConfigureAwait(false);
                if (canResume && CanResume)
                {
                    await ResumeAsync(ct).ConfigureAwait(false);
                }
                else
                {
                    ClearSession();
                    await IdentifyAsync(ct).ConfigureAwait(false);
                }
                break;

            case 10: // Hello
                _heartbeatIntervalMs = payload.d.GetProperty("heartbeat_interval").GetInt32();
                StartHeartbeat();
                if (CanResume)
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
        }
    }

    private async Task HandleCloseFrameAsync(WebSocketReceiveResult result, CancellationToken ct)
    {
        int closeCode = (int)(result.CloseStatus ?? WebSocketCloseStatus.Empty);
        WebSocketException closeError = new(closeCode, $"Close: {closeCode} - {result.CloseStatusDescription}");
        Error?.Invoke(this, closeError);

        if (IsFatalCloseCode(closeCode))
        {
            _autoReconnect = false;
            StopHeartbeat();
            Disconnected?.Invoke(this, closeError);
            await _internalCts.CancelAsync();
            return;
        }

        TimeSpan? delay = null;
        if (IsSessionEndingCloseCode(closeCode))
        {
            ClearSession();
        }
        else if (closeCode == 4003)
        {
            delay = InvalidSessionCloseDelay;

            // First try to resume. If that session is rejected as well, start clean.
            if (_sessionExpired)
            {
                ClearSession();
                SessionReset?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                _sessionExpired = true;
            }
        }

        await ReconnectAsync(closeError, delay, ct).ConfigureAwait(false);
    }
}
