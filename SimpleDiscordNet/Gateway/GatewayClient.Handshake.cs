using System.Net.WebSockets;
using System.Text.Json;

namespace SimpleDiscordNet.Gateway;

internal sealed partial class GatewayClient
{
    private async Task IdentifyAsync()
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
        await _ws.SendAsync(buffer.WrittenMemory, WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task ResumeAsync()
    {
        if (string.IsNullOrEmpty(_sessionId))
        {
            await IdentifyAsync().ConfigureAwait(false);
            return;
        }
        Resume resume = new()
        {
            d = new ResumePayload
            {
                token = token,
                session_id = _sessionId!,
                seq = _seq
            }
        };
        System.Buffers.ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            JsonSerializer.Serialize(writer, resume, json);
        }
        await _ws.SendAsync(buffer.WrittenMemory, WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Request all members for a guild via gateway. Discord will respond with GUILD_MEMBERS_CHUNK events.
    /// Requires GuildMembers intent.
    /// </summary>
    public async Task RequestGuildMembersAsync(string guildId)
    {
        if (_ws.State != WebSocketState.Open) return;

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
        await _ws.SendAsync(buffer.WrittenMemory, WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
    }
}
