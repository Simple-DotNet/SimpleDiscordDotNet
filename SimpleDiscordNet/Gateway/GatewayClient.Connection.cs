using System.Net.WebSockets;

namespace SimpleDiscordNet.Gateway;

internal sealed partial class GatewayClient
{
    private async Task ConnectSocketAsync(CancellationToken ct)
    {
        ClientWebSocket old = _ws;
        try { old.Dispose(); } catch { /* WebSocket disposal can throw, safe to ignore */ }
        ClientWebSocket ws = new();
        ws.Options.SetRequestHeader("User-Agent", "SimpleDiscordDotNet (https://example, 1.0)");
        await ws.ConnectAsync(new Uri("wss://gateway.discord.gg/?v=10&encoding=json"), ct).ConfigureAwait(false);
        _ws = ws;
        _reconnectAttempt = 0;
        _awaitingHeartbeatAck = false;
        Interlocked.Exchange(ref _missedHeartbeatAcks, 0);
    }

    private int GetBackoffDelayMs()
    {
        // Exponential backoff capped at 30s with jitter
        int baseMs = (int)Math.Min(30000, 1000 * Math.Pow(2, Math.Min(8, _reconnectAttempt)));
        int jitter = _rand.Next(0, 500);
        return baseMs + jitter;
    }

    private async Task<bool> SafeReconnectAsync(CancellationToken ct)
    {
        if (!await _reconnectGate.WaitAsync(0, ct).ConfigureAwait(false)) return false;
        try
        {
            lock (_heartbeatLock)
            {
                try { _ctsHeartbeat?.Cancel(); } catch { /* Cancellation may throw, safe to ignore */ }
            }
            try
            {
                ClientWebSocket ws = _ws;
                if (ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "reconnect", CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch { /* WebSocket close can throw if already closed, safe to ignore */ }

            // Backoff before reconnection
            _reconnectAttempt++;
            int delay = GetBackoffDelayMs();
            await Task.Delay(delay, ct).ConfigureAwait(false);

            await ConnectSocketAsync(ct).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _reconnectGate.Release();
        }
    }
}
