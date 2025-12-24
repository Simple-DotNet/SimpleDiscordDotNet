using System.Net.WebSockets;

namespace SimpleDiscordNet.Gateway;

internal sealed partial class GatewayClient
{
    private async Task ConnectSocketAsync(CancellationToken ct)
    {
        _ws = new ClientWebSocket();
        _ws.Options.SetRequestHeader("User-Agent", "SimpleDiscordDotNet (https://example, 1.0)");
        await _ws.ConnectAsync(new Uri("wss://gateway.discord.gg/?v=10&encoding=json"), ct).ConfigureAwait(false);
        _reconnectAttempt = 0;
        _awaitingHeartbeatAck = false;
        _missedHeartbeatAcks = 0;
    }

    private int GetBackoffDelayMs()
    {
        // Exponential backoff capped at 30s with jitter
        int baseMs = (int)Math.Min(30000, 1000 * Math.Pow(2, Math.Min(8, _reconnectAttempt)));
        int jitter = _rand.Next(0, 500);
        return baseMs + jitter;
    }

    private async Task SafeReconnectAsync(CancellationToken ct)
    {
        if (Interlocked.Exchange(ref _reconnecting, 1) == 1) return;
        try
        {
            try { _heartbeatTimer?.Dispose(); } catch { /* Timer disposal can throw, safe to ignore */ }
            try
            {
                if (_ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "reconnect", CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch { /* WebSocket close can throw if already closed, safe to ignore */ }

            // Backoff before reconnection
            _reconnectAttempt++;
            int delay = GetBackoffDelayMs();
            await Task.Delay(delay, ct).ConfigureAwait(false);

            await ConnectSocketAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _reconnecting, 0);
        }
    }
}
