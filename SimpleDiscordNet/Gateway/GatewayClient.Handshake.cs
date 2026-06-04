using System.Net.WebSockets;
using System.Text.Json;

namespace SimpleDiscordNet.Gateway;

internal sealed partial class GatewayClient
{
    private async Task IdentifyAsync(CancellationToken ct)
    {
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
        if (string.IsNullOrEmpty(_sessionId))
        {
            Interlocked.Exchange(ref _isReady, 0);
            await IdentifyAsync(ct).ConfigureAwait(false);
            return;
        }
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
