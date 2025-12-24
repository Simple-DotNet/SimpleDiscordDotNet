using System.Collections.Concurrent;
using System.Text.Json;
using SimpleDiscordNet.Gateway;
using SimpleDiscordNet.Logging;
using SimpleDiscordNet.Events;
using SimpleDiscordNet.Models;

namespace SimpleDiscordNet.Sharding;

/// <summary>
/// Manages lifecycle of all local shards in both SingleProcess and Distributed modes.
/// Handles connection, reconnection, and metrics collection for shards running on this machine.
/// Example: var manager = new ShardManager(token, intents, json, 0, 4); // Shard 0 of 4
/// </summary>
internal sealed class ShardManager : IDisposable
{
    private readonly string _token;
    private readonly DiscordIntents _intents;
    private readonly JsonSerializerOptions _json;
    private readonly NativeLogger _logger;
    private readonly ConcurrentDictionary<int, Shard> _shards = new();
    private volatile bool _disposed;

    public ShardManager(string token, DiscordIntents intents, JsonSerializerOptions json, NativeLogger logger)
    {
        _token = token;
        _intents = intents;
        _json = json;
        _logger = logger;
    }

    /// <summary>
    /// Creates and starts a shard with the given ID and total shard count.
    /// Example: await manager.StartShardAsync(0, 4, ct); // Start shard 0 of 4
    /// </summary>
    public async Task<Shard> StartShardAsync(int shardId, int totalShards, CancellationToken ct = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ShardManager));

        var shard = new Shard(shardId, totalShards, _token, _intents, _json, _logger);
        if (!_shards.TryAdd(shardId, shard))
        {
            shard.Dispose();
            throw new InvalidOperationException($"Shard {shardId} is already running");
        }

        try
        {
            await shard.ConnectAsync(ct).ConfigureAwait(false);
            _logger.Log(LogLevel.Information, $"Shard {shardId}/{totalShards} connected successfully");
            return shard;
        }
        catch
        {
            _shards.TryRemove(shardId, out _);
            shard.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Stops and removes a shard by ID.
    /// Example: await manager.StopShardAsync(0); // Stop shard 0
    /// </summary>
    public async Task StopShardAsync(int shardId)
    {
        if (_shards.TryRemove(shardId, out Shard? shard))
        {
            try
            {
                await shard.DisconnectAsync().ConfigureAwait(false);
                _logger.Log(LogLevel.Information, $"Shard {shardId} stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, $"Error stopping shard {shardId}: {ex.Message}", ex);
            }
            finally
            {
                shard.Dispose();
            }
        }
    }

    /// <summary>
    /// Gets a shard by ID, or null if not found.
    /// Example: var shard = manager.GetShard(0);
    /// </summary>
    public Shard? GetShard(int shardId)
    {
        _shards.TryGetValue(shardId, out var shard);
        return shard;
    }

    /// <summary>
    /// Gets information about all running shards.
    /// Example: var infos = manager.GetAllShardInfos(); // Returns ShardInfo array
    /// </summary>
    public ShardInfo[] GetAllShardInfos()
    {
        ShardInfo[] result = new ShardInfo[_shards.Count];
        int i = 0;
        foreach (KeyValuePair<int, Shard> kvp in _shards)
        {
            result[i++] = kvp.Value.ToInfo();
        }
        return result;
    }

    /// <summary>
    /// Gets the total number of shards running on this machine.
    /// Example: int count = manager.ShardCount;
    /// </summary>
    public int ShardCount => _shards.Count;

    /// <summary>
    /// Gets all shard IDs running on this machine.
    /// Example: var ids = manager.GetShardIds(); // Returns [0, 1, 2]
    /// </summary>
    public int[] GetShardIds()
    {
        return _shards.Keys.ToArray();
    }

    /// <summary>
    /// Stops all shards and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (KeyValuePair<int, Shard> kvp in _shards)
        {
            try
            {
                kvp.Value.Dispose();
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, $"Error disposing shard {kvp.Key}: {ex.Message}", ex);
            }
        }
        _shards.Clear();
    }

    /// <summary>
    /// Wires gateway events from a shard to provided handlers.
    /// Example: manager.WireShardEvents(shard, OnMessageCreate, OnInteractionCreate, ...);
    /// </summary>
    internal static void WireShardEvents(
        Shard shard,
        EventHandler? onConnected,
        EventHandler<Exception?>? onDisconnected,
        EventHandler<Exception>? onError,
        EventHandler<MessageCreateEventRaw>? onMessageCreate,
        EventHandler<InteractionCreateEvent>? onInteractionCreate,
        EventHandler<Events.GuildCreateEvent>? onGuildCreate,
        EventHandler<Entities.DiscordGuild>? onGuildUpdate,
        EventHandler<ulong>? onGuildDelete,
        EventHandler<Events.GuildEmojisUpdateEvent>? onGuildEmojisUpdate,
        EventHandler<Events.VoiceStateUpdateEvent>? onVoiceStateUpdate,
        EventHandler<Events.PresenceUpdateEvent>? onPresenceUpdate,
        EventHandler<Events.TypingStartEvent>? onTypingStart,
        EventHandler<Events.WebhooksUpdateEvent>? onWebhooksUpdate,
        EventHandler<Events.InviteCreateEvent>? onInviteCreate,
        EventHandler<Events.InviteDeleteEvent>? onInviteDelete,
        EventHandler<Events.GuildIntegrationsUpdateEvent>? onGuildIntegrationsUpdate,
        EventHandler<Entities.DiscordChannel>? onChannelCreate,
        EventHandler<Entities.DiscordChannel>? onChannelUpdate,
        EventHandler<Entities.DiscordChannel>? onChannelDelete,
        EventHandler<Events.GatewayRoleEvent>? onGuildRoleCreate,
        EventHandler<Events.GatewayRoleEvent>? onGuildRoleUpdate,
        EventHandler<Events.GatewayRoleEvent>? onGuildRoleDelete,
        EventHandler<Entities.DiscordChannel>? onThreadCreate,
        EventHandler<Entities.DiscordChannel>? onThreadUpdate,
        EventHandler<Entities.DiscordChannel>? onThreadDelete,
        EventHandler<Events.GatewayMemberEvent>? onGuildMemberAdd,
        EventHandler<Events.GatewayMemberEvent>? onGuildMemberUpdate,
        EventHandler<Events.GatewayMemberEvent>? onGuildMemberRemove,
        EventHandler<Events.GuildMembersChunkEvent>? onGuildMembersChunk,
        EventHandler<Events.GatewayUserEvent>? onGuildBanAdd,
        EventHandler<Events.GatewayUserEvent>? onGuildBanRemove,
        EventHandler<Entities.DiscordUser>? onUserUpdate,
        EventHandler<Events.GatewayAuditLogEvent>? onGuildAuditLogEntryCreate,
        EventHandler<Events.MessageUpdateEvent>? onMessageUpdate,
        EventHandler<Events.MessageEvent>? onMessageDelete,
        EventHandler<Events.MessageEvent>? onMessageDeleteBulk,
        EventHandler<Events.ReactionEvent>? onMessageReactionAdd,
        EventHandler<Events.ReactionEvent>? onMessageReactionRemove,
        EventHandler<Events.MessageEvent>? onMessageReactionRemoveAll,
        EventHandler<Events.ReactionEvent>? onMessageReactionRemoveEmoji)
    {
        var gateway = shard.Gateway;
        if (onConnected != null) gateway.Connected += onConnected;
        if (onDisconnected != null) gateway.Disconnected += onDisconnected;
        if (onError != null) gateway.Error += onError;
        if (onMessageCreate != null) gateway.MessageCreate += onMessageCreate;
        if (onInteractionCreate != null) gateway.InteractionCreate += onInteractionCreate;
        if (onGuildCreate != null) gateway.GuildCreate += onGuildCreate;
        if (onGuildUpdate != null) gateway.GuildUpdate += onGuildUpdate;
        if (onGuildDelete != null) gateway.GuildDelete += onGuildDelete;
        if (onGuildEmojisUpdate != null) gateway.GuildEmojisUpdate += onGuildEmojisUpdate;
        if (onVoiceStateUpdate != null) gateway.VoiceStateUpdate += onVoiceStateUpdate;
        if (onPresenceUpdate != null) gateway.PresenceUpdate += onPresenceUpdate;
        if (onTypingStart != null) gateway.TypingStart += onTypingStart;
        if (onWebhooksUpdate != null) gateway.WebhooksUpdate += onWebhooksUpdate;
        if (onInviteCreate != null) gateway.InviteCreate += onInviteCreate;
        if (onInviteDelete != null) gateway.InviteDelete += onInviteDelete;
        if (onGuildIntegrationsUpdate != null) gateway.GuildIntegrationsUpdate += onGuildIntegrationsUpdate;
        if (onChannelCreate != null) gateway.ChannelCreate += onChannelCreate;
        if (onChannelUpdate != null) gateway.ChannelUpdate += onChannelUpdate;
        if (onChannelDelete != null) gateway.ChannelDelete += onChannelDelete;
        if (onGuildRoleCreate != null) gateway.GuildRoleCreate += onGuildRoleCreate;
        if (onGuildRoleUpdate != null) gateway.GuildRoleUpdate += onGuildRoleUpdate;
        if (onGuildRoleDelete != null) gateway.GuildRoleDelete += onGuildRoleDelete;
        if (onThreadCreate != null) gateway.ThreadCreate += onThreadCreate;
        if (onThreadUpdate != null) gateway.ThreadUpdate += onThreadUpdate;
        if (onThreadDelete != null) gateway.ThreadDelete += onThreadDelete;
        if (onGuildMemberAdd != null) gateway.GuildMemberAdd += onGuildMemberAdd;
        if (onGuildMemberUpdate != null) gateway.GuildMemberUpdate += onGuildMemberUpdate;
        if (onGuildMemberRemove != null) gateway.GuildMemberRemove += onGuildMemberRemove;
        if (onGuildMembersChunk != null) gateway.GuildMembersChunk += onGuildMembersChunk;
        if (onGuildBanAdd != null) gateway.GuildBanAdd += onGuildBanAdd;
        if (onGuildBanRemove != null) gateway.GuildBanRemove += onGuildBanRemove;
        if (onUserUpdate != null) gateway.UserUpdate += onUserUpdate;
        if (onGuildAuditLogEntryCreate != null) gateway.GuildAuditLogEntryCreate += onGuildAuditLogEntryCreate;
        if (onMessageUpdate != null) gateway.MessageUpdate += onMessageUpdate;
        if (onMessageDelete != null) gateway.MessageDelete += onMessageDelete;
        if (onMessageDeleteBulk != null) gateway.MessageDeleteBulk += onMessageDeleteBulk;
        if (onMessageReactionAdd != null) gateway.MessageReactionAdd += onMessageReactionAdd;
        if (onMessageReactionRemove != null) gateway.MessageReactionRemove += onMessageReactionRemove;
        if (onMessageReactionRemoveAll != null) gateway.MessageReactionRemoveAll += onMessageReactionRemoveAll;
        if (onMessageReactionRemoveEmoji != null) gateway.MessageReactionRemoveEmoji += onMessageReactionRemoveEmoji;
    }
}
