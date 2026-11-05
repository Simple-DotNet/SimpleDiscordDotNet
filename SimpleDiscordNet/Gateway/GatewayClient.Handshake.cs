using System.Net.WebSockets;
using System.Text.Json;

namespace SimpleDiscordNet.Gateway;

internal sealed partial class GatewayClient
{
    // Discord allows 120 sends per 60 seconds on a connection. Staying under it leaves room for
    // heartbeats, which skip this limit because a late heartbeat would drop the connection.
    private const int MaxOutboundEventsPerWindow = 110;
    private static readonly TimeSpan OutboundWindow = TimeSpan.FromSeconds(60);

    private readonly Queue<long> _recentSends = new();

    private void ResetSendWindow()
    {
        lock (_recentSends)
        {
            _recentSends.Clear();
        }
    }

    /// <summary>
    /// Waits until another rate-limited event can be sent on this connection.
    /// </summary>
    private async Task WaitForSendSlotAsync(CancellationToken ct)
    {
        while (true)
        {
            long waitMs;
            lock (_recentSends)
            {
                long now = Environment.TickCount64;
                long windowMs = (long)OutboundWindow.TotalMilliseconds;
                while (_recentSends.Count > 0 && now - _recentSends.Peek() >= windowMs)
                {
                    _recentSends.Dequeue();
                }

                if (_recentSends.Count < MaxOutboundEventsPerWindow)
                {
                    _recentSends.Enqueue(now);
                    return;
                }

                waitMs = windowMs - (now - _recentSends.Peek());
            }

            await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(1, waitMs)), ct).ConfigureAwait(false);
        }
    }

    private async Task IdentifyAsync(CancellationToken ct)
    {
        if (identifyGate is not null && ShardId is { } shard)
        {
            await identifyGate(shard, ct).ConfigureAwait(false);
        }

        Identify identify = new()
        {
            d = new IdentifyPayload
            {
                token = token,
                intents = (int)intents,
                properties = new IdentifyConnectionProperties(),
                shard = ShardId.HasValue && TotalShards.HasValue ? [ShardId.Value, TotalShards.Value] : null
            }
        };
        System.Buffers.ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            JsonSerializer.Serialize(writer, identify, json);
        }
        LogGatewaySend(2, buffer.WrittenSpan);
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ClientWebSocket ws = _ws;
            await ws.SendAsync(buffer.WrittenMemory, WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
        }
        finally { _writeLock.Release(); }
    }

    private async Task ResumeAsync(CancellationToken ct)
    {
        Resume resume = new()
        {
            d = new ResumePayload
            {
                token = token,
                session_id = _sessionId!,
                seq = Interlocked.Read(ref _seq)
            }
        };
        System.Buffers.ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            JsonSerializer.Serialize(writer, resume, json);
        }
        LogGatewaySend(6, buffer.WrittenSpan);
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ClientWebSocket ws = _ws;
            await ws.SendAsync(buffer.WrittenMemory, WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
        }
        finally { _writeLock.Release(); }
    }

    /// <summary>
    /// Request all members for a guild via gateway. Discord will respond with GUILD_MEMBERS_CHUNK events.
    /// Requires GuildMembers intent.
    /// </summary>
    public async Task RequestGuildMembersAsync(string guildId, CancellationToken ct = default)
    {
        RequestGuildMembers request = new()
        {
            d = new RequestGuildMembersPayload
            {
                guild_id = guildId,
                query = string.Empty, // empty = all members
                limit = 0 // 0 = no limit
            }
        };
        System.Buffers.ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            JsonSerializer.Serialize(writer, request, json);
        }
        LogGatewaySend(8, buffer.WrittenSpan);
        await WaitForSendSlotAsync(ct).ConfigureAwait(false);
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ClientWebSocket ws = _ws;
            if (ws.State != WebSocketState.Open)
            {
                Error?.Invoke(this, new InvalidOperationException("Cannot request guild members: WebSocket is not open."));
                return;
            }
            await ws.SendAsync(buffer.WrittenMemory, WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, ex);
        }
        finally { _writeLock.Release(); }
    }

    /// <summary>
    /// Update the bot's presence/status via gateway.
    /// </summary>
    public async Task UpdatePresenceAsync(string status, BotActivity[]? activities, long? since = null, bool afk = false, CancellationToken ct = default)
    {
        UpdatePresence presence = new()
        {
            d = new UpdatePresencePayload
            {
                since = since,
                activities = activities,
                status = status,
                afk = afk
            }
        };
        System.Buffers.ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            JsonSerializer.Serialize(writer, presence, json);
        }
        LogGatewaySend(3, buffer.WrittenSpan);
        await WaitForSendSlotAsync(ct).ConfigureAwait(false);
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ClientWebSocket ws = _ws;
            if (ws.State != WebSocketState.Open)
            {
                Error?.Invoke(this, new InvalidOperationException("Cannot update presence: WebSocket is not open."));
                return;
            }
            await ws.SendAsync(buffer.WrittenMemory, WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, ex);
        }
        finally { _writeLock.Release(); }
    }
}
