using System.Net.WebSockets;
using SimpleDiscordNet.Core;

namespace SimpleDiscordNet.Gateway;

internal sealed partial class GatewayClient
{
    private const string GatewayQuery = "?v=10&encoding=json";

    // Closing with 1000 or 1001 ends the session on Discord's side, so reconnects use a 4000-range code
    private const WebSocketCloseStatus ResumableCloseStatus = (WebSocketCloseStatus)4000;

    private static readonly Uri DefaultGatewayUri = new("wss://gateway.discord.gg/" + GatewayQuery);
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(2);

    private readonly Uri _gatewayUri = gatewayUri ?? DefaultGatewayUri;

    private bool CanResume => !string.IsNullOrEmpty(_sessionId) && Interlocked.Read(ref _seq) > 0;

    private Uri GetConnectUri()
    {
        string? resumeUrl = _resumeGatewayUrl;
        return CanResume && !string.IsNullOrEmpty(resumeUrl)
            ? new Uri(resumeUrl.TrimEnd('/') + "/" + GatewayQuery)
            : _gatewayUri;
    }

    private async Task ConnectSocketAsync(CancellationToken ct)
    {
        ClientWebSocket old = _ws;
        try { old.Dispose(); } catch { /* WebSocket disposal can throw, safe to ignore */ }

        ClientWebSocket ws = new();
        try
        {
            ws.Options.SetRequestHeader("User-Agent", LibraryInfo.UserAgent);
            await ws.ConnectAsync(GetConnectUri(), ct).ConfigureAwait(false);
        }
        catch
        {
            ws.Dispose();
            throw;
        }

        _ws = ws;
        _awaitingHeartbeatAck = false;
        Interlocked.Exchange(ref _missedHeartbeatAcks, 0);
        ResetSendWindow();
    }

    private int GetBackoffDelayMs()
    {
        // Exponential backoff capped at 30s with jitter
        int baseMs = (int)Math.Min(30000, 1000 * Math.Pow(2, Math.Min(8, Volatile.Read(ref _reconnectAttempt))));
        return baseMs + Random.Shared.Next(0, 500);
    }

    /// <summary>
    /// Forgets the session so the next connection identifies instead of resuming, and lets
    /// Connected fire again on the READY that follows.
    /// </summary>
    private void ClearSession()
    {
        _sessionId = null;
        _resumeGatewayUrl = null;
        Interlocked.Exchange(ref _seq, 0);
        Interlocked.Exchange(ref _isReady, 0);
    }

    /// <summary>
    /// Replaces the connection and keeps retrying until one is established. Only the receive
    /// loop calls this, which makes it the single owner of reconnecting.
    /// </summary>
    private async Task ReconnectAsync(Exception? reason, TimeSpan? delay, CancellationToken ct)
    {
        if (!_autoReconnect)
        {
            await _internalCts.CancelAsync();
            return;
        }

        StopHeartbeat();
        Disconnected?.Invoke(this, reason);
        await CloseSocketForReconnectAsync().ConfigureAwait(false);

        if (delay is { } wait)
        {
            await Task.Delay(wait, ct).ConfigureAwait(false);
        }

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(GetBackoffDelayMs(), ct).ConfigureAwait(false);
            Interlocked.Increment(ref _reconnectAttempt);

            try
            {
                await ConnectSocketAsync(ct).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, ex);
            }
        }
    }

    /// <summary>
    /// Closes without ending the session, then aborts. A dead connection never answers a close
    /// frame, so this can't wait for the other side.
    /// </summary>
    private async Task CloseSocketForReconnectAsync()
    {
        ClientWebSocket ws = _ws;
        try
        {
            if (ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                using CancellationTokenSource timeout = new(CloseTimeout);
                await _writeLock.WaitAsync(timeout.Token).ConfigureAwait(false);
                try
                {
                    await ws.CloseOutputAsync(ResumableCloseStatus, "reconnect", timeout.Token).ConfigureAwait(false);
                }
                finally
                {
                    _writeLock.Release();
                }
            }
        }
        catch
        {
            // The socket may already be dead, which is why we're reconnecting
        }

        try { ws.Abort(); } catch { /* Abort can throw on a disposed socket, safe to ignore */ }
    }
}
