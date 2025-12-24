using System.Collections.Concurrent;
using System.Text.Json;
using SimpleDiscordNet.Context;
using SimpleDiscordNet.Core;
using SimpleDiscordNet.Commands;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Gateway;
using SimpleDiscordNet.Logging;
using SimpleDiscordNet.Models;
using SimpleDiscordNet.Models.Requests;
using SimpleDiscordNet.Rest;
using SimpleDiscordNet.Events;
using SimpleDiscordNet.Primitives;
using SimpleDiscordNet.Sharding;

namespace SimpleDiscordNet;

/// <summary>
/// Entry point for creating and running a Discord bot. Use <see cref="Builder"/> to configure.
/// </summary>
public sealed class DiscordBot : IDiscordBot
{
    private readonly string _token;
    private readonly DiscordIntents _intents;
    private readonly JsonSerializerOptions _json;
    private readonly NativeLogger _logger;
    private readonly RestClient _rest;
    private readonly GatewayClient? _gateway; // For non-sharded mode
    private readonly ShardManager? _shardManager; // For SingleProcess sharding
    private readonly ShardCoordinator? _coordinator; // For Distributed coordinator
    private readonly DistributedWorker? _worker; // For Distributed worker
    private readonly SlashCommandService _slashCommands;
    private readonly ComponentService _components;
    private readonly CancellationTokenSource _cts = new();
    private readonly EntityCache _cache = new();

    private DiscordUser? _botUser; // Bot's own user object

    private readonly bool _preloadGuilds;
    private readonly bool _preloadChannels;
    private readonly bool _preloadMembers;
    private readonly bool _autoLoadFullGuildData;

    private readonly bool _developmentMode;
    private readonly string[] _developmentGuildIds;

    private readonly List<IGeneratedManifest> _generatedManifests = [];

    private readonly ShardMode _shardMode;
    private readonly int? _shardId;
    private readonly int? _totalShards;
    private readonly string? _coordinatorUrl;
    private readonly string? _workerListenUrl;
    private readonly string? _workerId;

    private volatile bool _started;

    // Track pending member chunk requests to know when the guild is fully loaded
    private readonly ConcurrentDictionary<ulong, int> _pendingMemberChunks = new();

    private DiscordBot(
        string token,
        DiscordIntents intents,
        JsonSerializerOptions json,
        NativeLogger logger,
        TimeProvider? timeProvider,
        bool preloadGuilds,
        bool preloadChannels,
        bool preloadMembers,
        bool autoLoadFullGuildData,
        bool developmentMode,
        IEnumerable<string>? developmentGuildIds,
        ShardMode shardMode,
        int? shardId,
        int? totalShards,
        string? coordinatorUrl,
        string? workerListenUrl,
        string? workerId,
        bool isOriginalCoordinator)
    {
        _token = token;
        _intents = intents;
        _json = json;
        _logger = logger;
        _preloadGuilds = preloadGuilds;
        _preloadChannels = preloadChannels;
        _preloadMembers = preloadMembers;
        _autoLoadFullGuildData = autoLoadFullGuildData;
        _developmentMode = developmentMode;
        _developmentGuildIds = developmentGuildIds?.Where(static s => !string.IsNullOrWhiteSpace(s))
                                                  .Select(static s => s.Trim())
                                                  .Distinct(StringComparer.Ordinal)
                                                  .ToArray() ?? [];
        _shardMode = shardMode;
        _shardId = shardId;
        _totalShards = totalShards;
        _coordinatorUrl = coordinatorUrl;
        _workerListenUrl = workerListenUrl;
        _workerId = workerId;

        HttpClient httpClient = new(new SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
            EnableMultipleHttp2Connections = true,
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            ConnectTimeout = TimeSpan.FromSeconds(20)
        })
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        RateLimiter rateLimiter = new(timeProvider ?? TimeProvider.System);
        _rest = new RestClient(httpClient, token, json, logger, rateLimiter);

        // Initialize based on shard mode
        switch (shardMode)
        {
            case ShardMode.SingleProcess when shardId.HasValue && totalShards.HasValue:
                // Single process with explicit sharding
                _shardManager = new ShardManager(token, intents, json, logger);
                break;

            case ShardMode.Distributed when isOriginalCoordinator:
                // Distributed coordinator with HTTPS
                _coordinator = new ShardCoordinator(token, workerListenUrl ?? "https://+:8443/", logger, isOriginalCoordinator: true);
                break;

            case ShardMode.Distributed:
                // Distributed worker
                workerId ??= $"{Environment.MachineName}-{Guid.NewGuid():N}";
                _worker = new DistributedWorker(token, intents, json, logger, workerId, workerListenUrl ?? "http://+:8080/", coordinatorUrl ?? throw new ArgumentNullException(nameof(coordinatorUrl)));
                break;

            default:
                // Default: Single gateway (no sharding)
                _gateway = new GatewayClient(token, intents, json);
                break;
        }

        _slashCommands = new SlashCommandService(logger);
        _components = new ComponentService(logger);

        // Centralized wiring
        WireGatewayEvents();
    }

    /// <summary>
    /// The bot's own user object. Available after connection.
    /// Use this to identify if a message/event was triggered by the bot itself.
    /// Example: if (msg.Author.Id == bot.BotUser?.Id) return; // Ignore self
    /// </summary>
    public DiscordUser? BotUser => _botUser;

    // Events are surfaced via static SimpleDiscordNet.Events.DiscordEvents

    /// <summary>
    /// Starts the bot, connects to the gateway, and begins processing events.
    /// Example: await bot.StartAsync();
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started) return;
        _started = true;

        // Diagnostics: surface missing DM intent early
        if ((_intents & DiscordIntents.DirectMessages) == 0)
        {
            _logger.Log(LogLevel.Warning, "DirectMessages intent is not enabled; MessageCreate will not fire for direct messages (DMs). Enable DiscordIntents.DirectMessages to receive DM events.");
        }

        // Register ambient provider so consumers can access cached data.
        DiscordContext.SetProvider(this, _botUser, _cache.SnapshotGuilds, _cache.SnapshotChannels, _cache.SnapshotMembers, _cache.SnapshotUsers, _cache.SnapshotRoles);

        // Optionally preload caches using REST (runs in background)
        if (_preloadGuilds || _preloadChannels || _preloadMembers)
        {
            _ = Task.Run(() => PreloadAsync(_cts.Token), cancellationToken);
        }

        // Generated handlers (if any) are registered during Build() via GeneratedRegistry providers.

        // In development mode, immediately sync all current slash commands to configured guilds
        if (_developmentMode)
        {
            try
            {
                if (_developmentGuildIds.Length == 0)
                {
                    _logger.Log(LogLevel.Warning, "DevelopmentMode is enabled but no DevelopmentGuildIds were provided. Skipping command sync.");
                }
                else
                {
                    await SyncSlashCommandsAsync(_developmentGuildIds, cancellationToken).ConfigureAwait(false);
                    _logger.Log(LogLevel.Information, $"Development sync complete for { _developmentGuildIds.Length } guild(s).");
                }
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, $"Development command sync failed: {ex.Message}", ex);
            }
        }

        // Start appropriate component based on shard mode
        switch (_shardMode)
        {
            case ShardMode.SingleProcess when _shardManager != null && _shardId.HasValue && _totalShards.HasValue:
                // Start specific shard via ShardManager
                Shard shard = await _shardManager.StartShardAsync(_shardId.Value, _totalShards.Value, cancellationToken).ConfigureAwait(false);
                ShardManager.WireShardEvents(shard, OnConnected, OnDisconnected, OnError, null, OnInteractionCreate,
                    OnGuildCreate, OnGuildUpdate, OnGuildDelete, OnGuildEmojisUpdate,
                    OnVoiceStateUpdate, OnPresenceUpdate, OnTypingStart, OnWebhooksUpdate,
                    OnInviteCreate, OnInviteDelete, OnGuildIntegrationsUpdate,
                    OnChannelCreate, OnChannelUpdate, OnChannelDelete,
                    OnGuildRoleCreate, OnGuildRoleUpdate, OnGuildRoleDelete,
                    OnThreadCreate, OnThreadUpdate, OnThreadDelete,
                    OnGuildMemberAdd, OnGuildMemberUpdate, OnGuildMemberRemove, OnGuildMembersChunk,
                    OnGuildBanAdd, OnGuildBanRemove, OnUserUpdate, OnGuildAuditLogEntryCreate,
                    OnMessageUpdate, OnMessageDelete, OnMessageDeleteBulk,
                    OnMessageReactionAdd, OnMessageReactionRemove, OnMessageReactionRemoveAll, OnMessageReactionRemoveEmoji);
                break;

            case ShardMode.Distributed when _coordinator != null:
                // Start coordinator
                await _coordinator.StartAsync(cancellationToken).ConfigureAwait(false);
                break;

            case ShardMode.Distributed when _worker != null:
                // Start worker
                await _worker.StartAsync(cancellationToken).ConfigureAwait(false);
                // Wire events from worker's shards
                foreach (int workerId in _worker.ShardManager.GetShardIds())
                {
                    Shard? workerShard = _worker.ShardManager.GetShard(workerId);
                    if (workerShard != null)
                    {
                        ShardManager.WireShardEvents(workerShard, OnConnected, OnDisconnected, OnError, null, OnInteractionCreate,
                            OnGuildCreate, OnGuildUpdate, OnGuildDelete, OnGuildEmojisUpdate,
                            OnVoiceStateUpdate, OnPresenceUpdate, OnTypingStart, OnWebhooksUpdate,
                            OnInviteCreate, OnInviteDelete, OnGuildIntegrationsUpdate,
                            OnChannelCreate, OnChannelUpdate, OnChannelDelete,
                            OnGuildRoleCreate, OnGuildRoleUpdate, OnGuildRoleDelete,
                            OnThreadCreate, OnThreadUpdate, OnThreadDelete,
                            OnGuildMemberAdd, OnGuildMemberUpdate, OnGuildMemberRemove, OnGuildMembersChunk,
                            OnGuildBanAdd, OnGuildBanRemove, OnUserUpdate, OnGuildAuditLogEntryCreate,
                            OnMessageUpdate, OnMessageDelete, OnMessageDeleteBulk,
                            OnMessageReactionAdd, OnMessageReactionRemove, OnMessageReactionRemoveAll, OnMessageReactionRemoveEmoji);
                    }
                }
                break;

            default:
                // Start single gateway
                if (_gateway != null)
                    await _gateway.ConnectAsync(cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    /// Synchronous helper to start the bot. Prefer <see cref="StartAsync"/> in async contexts.
    /// </summary>
    public void Start()
    {
        StartAsync(_cts.Token).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Stops the bot and disposes of resources.
    /// </summary>
    public async Task StopAsync()
    {
        try
        {
            await _cts.CancelAsync();

            if (_gateway != null)
                await _gateway.DisconnectAsync().ConfigureAwait(false);

            if (_shardManager != null)
                _shardManager.Dispose();

            if (_coordinator != null)
                await _coordinator.StopAsync().ConfigureAwait(false);

            if (_worker != null)
                await _worker.StopAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            DiscordEvents.RaiseError(this, ex);
        }
        finally
        {
            await DisposeAsync();
        }
    }

    /// <summary>
    /// Synchronizes all registered slash commands to the specified guild ids.
    /// </summary>
    public async Task SyncSlashCommandsAsync(IEnumerable<string> guildIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(guildIds);
        ApplicationInfo app = await _rest.GetApplicationAsync(ct).ConfigureAwait(false)
                  ?? throw new InvalidOperationException("Failed to fetch application info");
        if (_generatedManifests.Count == 0)
            throw new InvalidOperationException("No generated command manifests were found. Ensure the source generator is referenced in the application project.");

        ApplicationCommandDefinition[] typed = _generatedManifests.SelectMany(m => m.Definitions).ToArray();
        object[] defs = typed.Cast<object>().ToArray();
        foreach (string gid in guildIds)
        {
            await _rest.PutGuildCommandsAsync(app.Id, gid, defs, ct).ConfigureAwait(false);
        }
    }

    // ----- Convenience REST APIs -----

    /// <summary>
    /// Sends a simple message to a channel with optional embed.
    /// </summary>
    public Task<DiscordMessage?> SendMessageAsync(string channelId, string content, EmbedBuilder? embed = null, CancellationToken ct = default)
    {
        CreateMessageRequest payload = new()
        {
            content = content,
            embeds = embed is null ? null : [embed.Build()]
        };
        return _rest.PostAsync<DiscordMessage>($"/channels/{channelId}/messages", payload, ct);
    }

    /// <summary>
    /// Sends a simple message to a channel with optional embed.
    /// </summary>
    public Task<DiscordMessage?> SendMessageAsync(ulong channelId, string content, EmbedBuilder? embed = null, CancellationToken ct = default)
        => SendMessageAsync(channelId.ToString(), content, embed, ct);

    /// <summary>
    /// Sends a simple message to a channel with optional embed.
    /// </summary>
    public Task<DiscordMessage?> SendMessageAsync(DiscordChannel channel, string content, EmbedBuilder? embed = null, CancellationToken ct = default)
        => SendMessageAsync(channel.Id, content, embed, ct);

    /// <summary>
    /// Sends a message using a MessageBuilder.
    /// </summary>
    public Task<DiscordMessage?> SendMessageAsync(string channelId, MessageBuilder builder, CancellationToken ct = default)
    {
        MessagePayload payload = builder.Build();
        var files = builder.GetFiles();

        CreateMessageRequest request = new()
        {
            content = payload.content,
            embeds = payload.embeds,
            components = payload.components,
            allowed_mentions = payload.allowed_mentions,
            attachments = payload.attachments
        };

        // If there are files, use multipart upload
        if (files is not null && files.Count > 0)
        {
            return _rest.PostMultipartAsync<DiscordMessage>($"/channels/{channelId}/messages", request, files, ct);
        }

        return _rest.PostAsync<DiscordMessage>($"/channels/{channelId}/messages", request, ct);
    }

    /// <summary>
    /// Sends a message using a MessageBuilder.
    /// </summary>
    public Task<DiscordMessage?> SendMessageAsync(ulong channelId, MessageBuilder builder, CancellationToken ct = default)
        => SendMessageAsync(channelId.ToString(), builder, ct);

    /// <summary>
    /// Sends a message using a MessageBuilder.
    /// </summary>
    public Task<DiscordMessage?> SendMessageAsync(DiscordChannel channel, MessageBuilder builder, CancellationToken ct = default)
        => SendMessageAsync(channel.Id, builder, ct);

    /// <summary>
    /// Sends a message with a single attachment.
    /// </summary>
    public Task<DiscordMessage?> SendAttachmentAsync(string channelId, string content, string fileName, ReadOnlyMemory<byte> data, EmbedBuilder? embed = null, CancellationToken ct = default)
    {
        CreateMessageRequest payload = new()
        {
            content = content,
            embeds = embed is null ? null : [embed.Build()],
            attachments = [new { id = 0, filename = fileName }]
        };
        return _rest.PostMultipartAsync<DiscordMessage>($"/channels/{channelId}/messages", payload, (fileName, data), ct);
    }

    /// <summary>
    /// Sends a message with a single attachment.
    /// </summary>
    public Task<DiscordMessage?> SendAttachmentAsync(ulong channelId, string content, string fileName, ReadOnlyMemory<byte> data, EmbedBuilder? embed = null, CancellationToken ct = default)
        => SendAttachmentAsync(channelId.ToString(), content, fileName, data, embed, ct);

    /// <summary>
    /// Sends a message with a single attachment.
    /// </summary>
    public Task<DiscordMessage?> SendAttachmentAsync(DiscordChannel channel, string content, string fileName, ReadOnlyMemory<byte> data, EmbedBuilder? embed = null, CancellationToken ct = default)
        => SendAttachmentAsync(channel.Id, content, fileName, data, embed, ct);

    /// <summary>
    /// Gets a guild by id.
    /// </summary>
    public Task<DiscordGuild?> GetGuildAsync(string guildId, CancellationToken ct = default)
        => _rest.GetAsync<DiscordGuild>($"/guilds/{guildId}", ct);

    /// <summary>
    /// Gets a guild by id.
    /// </summary>
    public Task<DiscordGuild?> GetGuildAsync(ulong guildId, CancellationToken ct = default)
        => GetGuildAsync(guildId.ToString(), ct);

    /// <summary>
    /// Gets channels of a guild.
    /// </summary>
    public async Task<IEnumerable<DiscordChannel>> GetGuildChannelsAsync(string guildId, CancellationToken ct = default)
    {
        var result = await _rest.GetAsync<DiscordChannel[]>($"/guilds/{guildId}/channels", ct);
        return result ?? [];
    }

    /// <summary>
    /// Gets channels of a guild.
    /// </summary>
    public Task<IEnumerable<DiscordChannel>> GetGuildChannelsAsync(ulong guildId, CancellationToken ct = default)
        => GetGuildChannelsAsync(guildId.ToString(), ct);

    /// <summary>
    /// Gets channels of a guild.
    /// </summary>
    public Task<IEnumerable<DiscordChannel>> GetGuildChannelsAsync(DiscordGuild guild, CancellationToken ct = default)
        => GetGuildChannelsAsync(guild.Id, ct);

    /// <summary>
    /// Gets roles of a guild.
    /// </summary>
    public async Task<IEnumerable<DiscordRole>> GetGuildRolesAsync(string guildId, CancellationToken ct = default)
    {
        var result = await _rest.GetAsync<DiscordRole[]>($"/guilds/{guildId}/roles", ct);
        return result ?? [];
    }

    /// <summary>
    /// Gets roles of a guild.
    /// </summary>
    public Task<IEnumerable<DiscordRole>> GetGuildRolesAsync(ulong guildId, CancellationToken ct = default)
        => GetGuildRolesAsync(guildId.ToString(), ct);

    /// <summary>
    /// Gets roles of a guild.
    /// </summary>
    public Task<IEnumerable<DiscordRole>> GetGuildRolesAsync(DiscordGuild guild, CancellationToken ct = default)
        => GetGuildRolesAsync(guild.Id, ct);

    /// <summary>
    /// Lists members of a guild with pagination support.
    /// </summary>
    public async Task<IEnumerable<DiscordMember>> ListGuildMembersAsync(string guildId, int limit = 1000, string? after = null, CancellationToken ct = default)
    {
        if (limit is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(limit));
        string route = $"/guilds/{guildId}/members?limit={limit}" + (after is null ? string.Empty : $"&after={after}");
        var result = await _rest.GetAsync<DiscordMember[]>(route, ct);
        return result ?? [];
    }

    public Task<IEnumerable<DiscordMember>> ListGuildMembersAsync(ulong guildId, int limit = 1000, string? after = null, CancellationToken ct = default)
        => ListGuildMembersAsync(guildId.ToString(), limit, after, ct);

    public Task<IEnumerable<DiscordMember>> ListGuildMembersAsync(DiscordGuild guild, int limit = 1000, string? after = null, CancellationToken ct = default)
        => ListGuildMembersAsync(guild.Id, limit, after, ct);

    /// <summary>
    /// Sets or updates a channel permission overwrite for a role or member.
    /// </summary>
    /// <param name="channelId">Channel ID</param>
    /// <param name="targetId">Role ID or User ID</param>
    /// <param name="type">0 = role, 1 = member</param>
    /// <param name="allow">Permission bits to allow (as ulong)</param>
    /// <param name="deny">Permission bits to deny (as ulong)</param>
    /// <param name="ct">Cancellation token</param>
    public Task SetChannelPermissionAsync(string channelId, string targetId, int type, ulong allow, ulong deny, CancellationToken ct = default)
    {
        var payload = new { type, allow = allow.ToString(), deny = deny.ToString() };
        return _rest.PutChannelPermissionAsync(channelId, targetId, payload, ct);
    }

    /// <summary>
    /// Deletes a channel permission overwrite.
    /// </summary>
    public Task DeleteChannelPermissionAsync(string channelId, string overwriteId, CancellationToken ct = default)
        => _rest.DeleteChannelPermissionAsync(channelId, overwriteId, ct);

    /// <summary>
    /// Creates a new role in a guild.
    /// </summary>
    /// <param name="guildId">Guild ID</param>
    /// <param name="name">Role name</param>
    /// <param name="permissions">Permission bits (as ulong)</param>
    /// <param name="color">Role color (RGB integer)</param>
    /// <param name="hoist">Whether to display role separately in sidebar</param>
    /// <param name="mentionable">Whether role is mentionable</param>
    /// <param name="ct">Cancellation token</param>
    public Task<DiscordRole?> CreateRoleAsync(string guildId, string? name = null, ulong? permissions = null, int? color = null, bool? hoist = null, bool? mentionable = null, CancellationToken ct = default)
    {
        var payload = new
        {
            name,
            permissions = permissions?.ToString(),
            color,
            hoist,
            mentionable
        };
        return _rest.PostGuildRoleAsync<DiscordRole>(guildId, payload, ct);
    }

    /// <summary>
    /// Modifies a role in a guild.
    /// </summary>
    public Task<DiscordRole?> ModifyRoleAsync(string guildId, string roleId, string? name = null, ulong? permissions = null, int? color = null, bool? hoist = null, bool? mentionable = null, CancellationToken ct = default)
    {
        var payload = new
        {
            name,
            permissions = permissions?.ToString(),
            color,
            hoist,
            mentionable
        };
        return _rest.PatchGuildRoleAsync<DiscordRole>(guildId, roleId, payload, ct);
    }

    /// <summary>
    /// Deletes a role from a guild.
    /// </summary>
    public Task DeleteRoleAsync(string guildId, string roleId, CancellationToken ct = default)
        => _rest.DeleteGuildRoleAsync(guildId, roleId, ct);

    /// <summary>
    /// Creates a new channel in a guild.
    /// </summary>
    /// <param name="guildId">Guild ID</param>
    /// <param name="name">Channel name</param>
    /// <param name="type">Channel type</param>
    /// <param name="parentId">Parent category ID (optional)</param>
    /// <param name="permissionOverwrites">Permission overwrites (optional)</param>
    /// <param name="ct">Cancellation token</param>
    public Task<DiscordChannel?> CreateChannelAsync(string guildId, string name, Entities.ChannelType type, string? parentId = null, object[]? permissionOverwrites = null, CancellationToken ct = default)
    {
        var payload = new
        {
            name,
            type = (int)type,
            parent_id = parentId,
            permission_overwrites = permissionOverwrites
        };
        return _rest.PostGuildChannelAsync<DiscordChannel>(guildId, payload, ct);
    }

    /// <summary>
    /// Creates a new text channel in a guild.
    /// </summary>
    public Task<DiscordChannel?> CreateTextChannelAsync(string guildId, string name, string? parentId = null, CancellationToken ct = default)
        => CreateChannelAsync(guildId, name, ChannelType.GuildText, parentId, ct: ct);

    /// <summary>
    /// Creates a new voice channel in a guild.
    /// </summary>
    public Task<DiscordChannel?> CreateVoiceChannelAsync(string guildId, string name, string? parentId = null, CancellationToken ct = default)
        => CreateChannelAsync(guildId, name, ChannelType.GuildVoice, parentId, ct: ct);

    /// <summary>
    /// Creates a new category channel in a guild.
    /// </summary>
    public Task<DiscordChannel?> CreateCategoryAsync(string guildId, string name, CancellationToken ct = default)
        => CreateChannelAsync(guildId, name, ChannelType.GuildCategory, ct: ct);

    /// <summary>
    /// Modifies a channel.
    /// </summary>
    public Task<DiscordChannel?> ModifyChannelAsync(string channelId, string? name = null, int? type = null, string? parentId = null, int? position = null, string? topic = null, bool? nsfw = null, int? bitrate = null, int? userLimit = null, int? rateLimitPerUser = null, CancellationToken ct = default)
    {
        var payload = new
        {
            name,
            type,
            parent_id = parentId,
            position,
            topic,
            nsfw,
            bitrate,
            user_limit = userLimit,
            rate_limit_per_user = rateLimitPerUser
        };
        return _rest.PatchChannelAsync<DiscordChannel>(channelId, payload, ct);
    }

    /// <summary>
    /// Deletes a channel.
    /// </summary>
    public Task DeleteChannelAsync(string channelId, CancellationToken ct = default)
        => _rest.DeleteChannelAsync(channelId, ct);

    // ---- Message management ----

    /// <summary>
    /// Gets a specific message from a channel.
    /// Example: var message = await bot.GetMessageAsync(channelId, messageId);
    /// </summary>
    public Task<DiscordMessage?> GetMessageAsync(string channelId, string messageId, CancellationToken ct = default)
        => _rest.GetChannelMessageAsync<DiscordMessage>(channelId, messageId, ct);

    /// <summary>
    /// Gets recent messages from a channel (up to 100).
    /// Example: foreach (var msg in await bot.GetMessagesAsync(channelId, limit: 50)) { }
    /// </summary>
    /// <param name="channelId">Channel ID</param>
    /// <param name="limit">Number of messages to retrieve (1-100, default 50)</param>
    /// <param name="before">Get messages before this message ID</param>
    /// <param name="after">Get messages after this message ID</param>
    public async Task<IEnumerable<DiscordMessage>> GetMessagesAsync(string channelId, int limit = 50, string? before = null, string? after = null, CancellationToken ct = default)
    {
        if (limit is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 100");

        var result = await _rest.GetChannelMessagesAsync<DiscordMessage[]>(channelId, limit, before, after, ct);
        return result ?? [];
    }

    /// <summary>
    /// Edits a message. Only works on messages sent by the bot.
    /// Example: await bot.EditMessageAsync(channelId, messageId, "Updated content");
    /// </summary>
    public Task<DiscordMessage?> EditMessageAsync(string channelId, string messageId, string content, EmbedBuilder? embed = null, CancellationToken ct = default)
    {
        var payload = new
        {
            content,
            embeds = embed is null ? null : new[] { embed.Build() }
        };
        return _rest.PatchMessageAsync<DiscordMessage>(channelId, messageId, payload, ct);
    }

    /// <summary>
    /// Deletes a message. Requires appropriate permissions.
    /// Example: await bot.DeleteMessageAsync(channelId, messageId);
    /// </summary>
    public Task DeleteMessageAsync(string channelId, string messageId, CancellationToken ct = default)
        => _rest.DeleteMessageAsync(channelId, messageId, ct);

    /// <summary>
    /// Bulk deletes multiple messages (2-100 messages, must be less than 14 days old).
    /// Example: await bot.BulkDeleteMessagesAsync(channelId, new[] { msgId1, msgId2, msgId3 });
    /// </summary>
    public Task BulkDeleteMessagesAsync(string channelId, string[] messageIds, CancellationToken ct = default)
    {
        if (messageIds.Length < 2 || messageIds.Length > 100)
            throw new ArgumentException("Must provide between 2 and 100 message IDs", nameof(messageIds));
        return _rest.BulkDeleteMessagesAsync(channelId, messageIds, ct);
    }

    // ---- Reaction management ----

    /// <summary>
    /// Adds a reaction to a message.
    /// Example (Unicode emoji): await bot.AddReactionAsync(channelId, messageId, "👍");
    /// Example (custom emoji): await bot.AddReactionAsync(channelId, messageId, "custom_emoji:123456789");
    /// </summary>
    /// <param name="channelId">Channel ID</param>
    /// <param name="messageId">Message ID</param>
    /// <param name="emoji">Unicode emoji or custom emoji in format "name:id"</param>
    public Task AddReactionAsync(string channelId, string messageId, string emoji, CancellationToken ct = default)
    {
        string encoded = System.Web.HttpUtility.UrlEncode(emoji);
        return _rest.AddReactionAsync(channelId, messageId, encoded, ct);
    }

    /// <summary>
    /// Adds a reaction to a message using an Emoji object.
    /// Example: await bot.AddReactionAsync(channelId, messageId, Emoji.Unicode("👍"));
    /// Example: await bot.AddReactionAsync(channelId, messageId, Emoji.Custom("custom", "123456789"));
    /// </summary>
    public Task AddReactionAsync(string channelId, string messageId, DiscordEmoji emoji, CancellationToken ct = default)
    {
        string encoded = System.Web.HttpUtility.UrlEncode(emoji.GetReactionFormat());
        return _rest.AddReactionAsync(channelId, messageId, encoded, ct);
    }

    /// <summary>
    /// Removes the bot's own reaction from a message.
    /// Example: await bot.RemoveOwnReactionAsync(channelId, messageId, "👍");
    /// </summary>
    public Task RemoveOwnReactionAsync(string channelId, string messageId, string emoji, CancellationToken ct = default)
    {
        string encoded = System.Web.HttpUtility.UrlEncode(emoji);
        return _rest.RemoveOwnReactionAsync(channelId, messageId, encoded, ct);
    }

    /// <summary>
    /// Removes a user's reaction from a message. Requires MANAGE_MESSAGES permission.
    /// Example: await bot.RemoveUserReactionAsync(channelId, messageId, "👍", userId);
    /// </summary>
    public Task RemoveUserReactionAsync(string channelId, string messageId, string emoji, string userId, CancellationToken ct = default)
    {
        string encoded = System.Web.HttpUtility.UrlEncode(emoji);
        return _rest.RemoveUserReactionAsync(channelId, messageId, encoded, userId, ct);
    }

    /// <summary>
    /// Gets users who reacted with a specific emoji (up to 100).
    /// Example: foreach (var user in await bot.GetReactionsAsync(channelId, messageId, "👍")) { }
    /// </summary>
    public async Task<IEnumerable<DiscordUser>> GetReactionsAsync(string channelId, string messageId, string emoji, int limit = 25, CancellationToken ct = default)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 100");
        string encoded = System.Web.HttpUtility.UrlEncode(emoji);
        var result = await _rest.GetReactionsAsync<DiscordUser[]>(channelId, messageId, encoded, limit, ct);
        return result ?? [];
    }

    /// <summary>
    /// Removes all reactions from a message. Requires MANAGE_MESSAGES permission.
    /// Example: await bot.RemoveAllReactionsAsync(channelId, messageId);
    /// </summary>
    public Task RemoveAllReactionsAsync(string channelId, string messageId, CancellationToken ct = default)
        => _rest.RemoveAllReactionsAsync(channelId, messageId, ct);

    /// <summary>
    /// Removes all reactions for a specific emoji from a message. Requires MANAGE_MESSAGES permission.
    /// Example: await bot.RemoveAllReactionsForEmojiAsync(channelId, messageId, "👍");
    /// </summary>
    public Task RemoveAllReactionsForEmojiAsync(string channelId, string messageId, string emoji, CancellationToken ct = default)
    {
        string encoded = System.Web.HttpUtility.UrlEncode(emoji);
        return _rest.RemoveAllReactionsForEmojiAsync(channelId, messageId, encoded, ct);
    }

    // ---- Pin management ----

    /// <summary>
    /// Pins a message in a channel. Requires MANAGE_MESSAGES permission. Max 50 pins per channel.
    /// Example: await bot.PinMessageAsync(channelId, messageId);
    /// </summary>
    public Task PinMessageAsync(string channelId, string messageId, CancellationToken ct = default)
        => _rest.PinMessageAsync(channelId, messageId, ct);

    /// <summary>
    /// Unpins a message in a channel. Requires MANAGE_MESSAGES permission.
    /// Example: await bot.UnpinMessageAsync(channelId, messageId);
    /// </summary>
    public Task UnpinMessageAsync(string channelId, string messageId, CancellationToken ct = default)
        => _rest.UnpinMessageAsync(channelId, messageId, ct);

    /// <summary>
    /// Gets all pinned messages in a channel (up to 50).
    /// Example: foreach (var msg in await bot.GetPinnedMessagesAsync(channelId)) { }
    /// </summary>
    public async Task<IEnumerable<DiscordMessage>> GetPinnedMessagesAsync(string channelId, CancellationToken ct = default)
    {
        var result = await _rest.GetPinnedMessagesAsync<DiscordMessage[]>(channelId, ct);
        return result ?? [];
    }

    // ---- Member moderation ----

    /// <summary>
    /// Kicks a member from the guild. Requires KICK_MEMBERS permission.
    /// Example: await bot.KickMemberAsync(guildId, userId);
    /// </summary>
    public Task KickMemberAsync(string guildId, string userId, CancellationToken ct = default)
        => _rest.KickMemberAsync(guildId, userId, ct);

    /// <summary>
    /// Bans a member from the guild. Requires BAN_MEMBERS permission.
    /// Example: await bot.BanMemberAsync(guildId, userId, deleteMessageDays: 7);
    /// </summary>
    /// <param name="guildId">Guild ID</param>
    /// <param name="userId">User ID to ban</param>
    /// <param name="deleteMessageDays">Number of days of messages to delete (0-7, optional)</param>
    public Task BanMemberAsync(string guildId, string userId, int? deleteMessageDays = null, CancellationToken ct = default)
    {
        if (deleteMessageDays.HasValue && (deleteMessageDays.Value < 0 || deleteMessageDays.Value > 7))
            throw new ArgumentOutOfRangeException(nameof(deleteMessageDays), "Must be between 0 and 7 days");
        return _rest.BanMemberAsync(guildId, userId, deleteMessageDays, ct);
    }

    /// <summary>
    /// Unbans a user from the guild. Requires BAN_MEMBERS permission.
    /// Example: await bot.UnbanMemberAsync(guildId, userId);
    /// </summary>
    public Task UnbanMemberAsync(string guildId, string userId, CancellationToken ct = default)
        => _rest.UnbanMemberAsync(guildId, userId, ct);

    // ---- Role assignment ----

    /// <summary>
    /// Adds a role to a guild member. Requires MANAGE_ROLES permission.
    /// Example: await bot.AddRoleToMemberAsync(guildId, userId, roleId);
    /// </summary>
    public Task AddRoleToMemberAsync(string guildId, string userId, string roleId, CancellationToken ct = default)
        => _rest.AddMemberRoleAsync(guildId, userId, roleId, ct);

    /// <summary>
    /// Removes a role from a guild member. Requires MANAGE_ROLES permission.
    /// Example: await bot.RemoveRoleFromMemberAsync(guildId, userId, roleId);
    /// </summary>
    public Task RemoveRoleFromMemberAsync(string guildId, string userId, string roleId, CancellationToken ct = default)
        => _rest.RemoveMemberRoleAsync(guildId, userId, roleId, ct);

    // ---- Typing indicator ----

    /// <summary>
    /// Triggers the typing indicator in a channel. Lasts 10 seconds or until a message is sent.
    /// Example: await bot.TriggerTypingAsync(channelId);
    /// </summary>
    public Task TriggerTypingAsync(string channelId, CancellationToken ct = default)
        => _rest.TriggerTypingIndicatorAsync(channelId, ct);

    // ---- Thread operations ----

    /// <summary>
    /// Joins a thread channel.
    /// Example: await bot.JoinThreadAsync(threadId);
    /// </summary>
    public Task JoinThreadAsync(string threadId, CancellationToken ct = default)
        => _rest.JoinThreadAsync(threadId, ct);

    /// <summary>
    /// Leaves a thread channel.
    /// Example: await bot.LeaveThreadAsync(threadId);
    /// </summary>
    public Task LeaveThreadAsync(string threadId, CancellationToken ct = default)
        => _rest.LeaveThreadAsync(threadId, ct);

    /// <summary>
    /// Adds a member to a thread. Requires MANAGE_THREADS permission.
    /// Example: await bot.AddThreadMemberAsync(threadId, userId);
    /// </summary>
    public Task AddThreadMemberAsync(string threadId, string userId, CancellationToken ct = default)
        => _rest.AddThreadMemberAsync(threadId, userId, ct);

    /// <summary>
    /// Removes a member from a thread. Requires MANAGE_THREADS permission.
    /// Example: await bot.RemoveThreadMemberAsync(threadId, userId);
    /// </summary>
    public Task RemoveThreadMemberAsync(string threadId, string userId, CancellationToken ct = default)
        => _rest.RemoveThreadMemberAsync(threadId, userId, ct);

    // ---- Simplified Helper APIs for Beginners ----

    /// <summary>
    /// Sends a message with buttons to a channel.
    /// Example: await bot.SendMessageWithButtonsAsync(channelId, "Click a button:", new Button("Yes", "yes_id"), new Button("No", "no_id"));
    /// </summary>
    public Task SendMessageWithButtonsAsync(string channelId, string content, params Button[] buttons)
    {
        CreateMessageRequest payload = new()
        {
            content = content,
            components = [new ActionRow(buttons.Cast<object>().ToArray())]
        };
        return _rest.PostAsync($"/channels/{channelId}/messages", payload, CancellationToken.None);
    }

    /// <summary>
    /// Sends an embed-only message to a channel.
    /// Example: await bot.SendEmbedAsync(channelId, new EmbedBuilder().WithTitle("Title").WithDescription("Description"));
    /// </summary>
    public Task SendEmbedAsync(string channelId, EmbedBuilder embed, CancellationToken ct = default)
        => SendMessageAsync(channelId, string.Empty, embed, ct);

    /// <summary>
    /// Gets a member from a guild by user ID.
    /// Example: var member = await bot.GetMemberAsync(guildId, userId);
    /// </summary>
    public Task<DiscordMember?> GetMemberAsync(string guildId, string userId, CancellationToken ct = default)
        => _rest.GetAsync<DiscordMember>($"/guilds/{guildId}/members/{userId}", ct);

    /// <summary>
    /// Gets a channel by ID.
    /// Example: var channel = await bot.GetChannelAsync(channelId);
    /// </summary>
    public Task<DiscordChannel?> GetChannelAsync(string channelId, CancellationToken ct = default)
        => _rest.GetAsync<DiscordChannel>($"/channels/{channelId}", ct);

    /// <summary>
    /// Gets information about the bot's application.
    /// Example: var app = await bot.GetApplicationInfoAsync();
    /// </summary>
    public Task<ApplicationInfo?> GetApplicationInfoAsync(CancellationToken ct = default)
        => _rest.GetApplicationAsync(ct);

    /// <summary>
    /// Updates the bot's nickname in a guild.
    /// Example: await bot.SetNicknameAsync(guildId, "CoolBot");
    /// </summary>
    public Task SetNicknameAsync(string guildId, string nickname, CancellationToken ct = default)
    {
        var payload = new { nick = nickname };
        return _rest.PatchAsync($"/guilds/{guildId}/members/@me", payload, ct);
    }

    /// <summary>
    /// Checks if a member has a specific role.
    /// Example: bool hasRole = await bot.MemberHasRoleAsync(guildId, userId, roleId);
    /// </summary>
    public async Task<bool> MemberHasRoleAsync(string guildId, string userId, string roleId, CancellationToken ct = default)
    {
        DiscordMember? member = await GetMemberAsync(guildId, userId, ct).ConfigureAwait(false);
        return member?.HasRole(ulong.Parse(roleId)) ?? false;
    }

    /// <summary>
    /// Gets the @everyone role for a guild.
    /// Example: var everyoneRole = await bot.GetEveryoneRoleAsync(guildId);
    /// </summary>
    public async Task<DiscordRole?> GetEveryoneRoleAsync(string guildId, CancellationToken ct = default)
    {
        var roles = await GetGuildRolesAsync(guildId, ct).ConfigureAwait(false);
        return roles.FirstOrDefault(r => r.Id == ulong.Parse(guildId));
    }

    /// <summary>
    /// Sends a direct message to a user by creating a DM channel and sending a message.
    /// Example: await bot.SendDMAsync(userId, "Hello!");
    /// </summary>
    public async Task<DiscordMessage?> SendDMAsync(string userId, string content, EmbedBuilder? embed = null, CancellationToken ct = default)
    {
        // Create DM channel first
        var dmPayload = new { recipient_id = userId };

        // Create the DM channel - Discord will return the existing one if it exists
        DiscordChannel? dmChannel = await _rest.PostAsync<DiscordChannel>("/users/@me/channels", dmPayload, ct).ConfigureAwait(false);

        if (dmChannel is not null)
        {
            // Cache the DM channel so it's available in DiscordContext
            // DM channels don't have a guild, so we use guildId of 0
            _cache.UpsertChannel(0, dmChannel);

            return await SendMessageAsync(dmChannel.Id.ToString(), content, embed, ct).ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>
    /// Pins a message in a channel.
    /// Example: await bot.PinMessageAsync(channelId, messageId);
    /// </summary>
    public Task PinMessageAsync(ulong channelId, ulong messageId, CancellationToken ct = default)
        => _rest.PutAsync($"/channels/{channelId}/pins/{messageId}", new { }, ct);

    /// <summary>
    /// Deletes a message from a channel.
    /// Example: await bot.DeleteMessageAsync(channelId, messageId);
    /// </summary>
    public Task DeleteMessageAsync(ulong channelId, ulong messageId, CancellationToken ct = default)
        => _rest.DeleteMessageAsync(channelId.ToString(), messageId.ToString(), ct);

    // Event handler methods for gateway events
    private void OnConnected(object? sender, EventArgs e) => DiscordEvents.RaiseConnected(this);
    private void OnDisconnected(object? sender, Exception? ex) => DiscordEvents.RaiseDisconnected(this, ex);
    private void OnError(object? sender, Exception ex) => DiscordEvents.RaiseError(this, ex);

    private void OnInteractionCreate(object? sender, InteractionCreateEvent e)
    {
        // Populate Guild from cache if this is a guild interaction
        InteractionCreateEvent enriched = e;
        if (e.GuildId is not null && ulong.TryParse(e.GuildId, out ulong guildId))
        {
            if (_cache.TryGetGuild(guildId, out DiscordGuild? guild))
            {
                enriched = e with { Guild = guild };
            }
        }

        DiscordEvents.RaiseInteractionCreated(this, enriched);

        switch (enriched)
        {
            case { Type: InteractionType.ApplicationCommand, Data: not null }:
                _ = _slashCommands.HandleAsync(enriched, _rest, _cts.Token);
                break;
            case { Type: InteractionType.MessageComponent, Component: not null } or { Type: InteractionType.ModalSubmit, Modal: not null }:
                _ = _components.HandleAsync(enriched, _rest, _cts.Token);
                break;
        }
    }

    private void OnGuildCreate(object? sender, GuildCreateEvent evt)
    {
        _cache.UpsertGuild(evt.Guild);

        // Cache channels from GUILD_CREATE
        if (evt.Channels is not null && evt.Channels.Length > 0)
        {
            _cache.SetChannels(evt.Guild.Id, evt.Channels);
        }

        // Cache members from GUILD_CREATE (partial list)
        if (evt.Members is not null && evt.Members.Length > 0)
        {
            _cache.SetMembers(evt.Guild.Id, evt.Members);
        }

        // Cache threads from GUILD_CREATE (threads are channels)
        if (evt.Threads is not null && evt.Threads.Length > 0)
        {
            foreach (DiscordChannel thread in evt.Threads)
            {
                _cache.UpsertChannel(evt.Guild.Id, thread);
            }
        }

        DiscordEvents.RaiseGuildAdded(this, new GuildEvent { Guild = evt.Guild });

        // Auto-load complete guild data if enabled
        if (_autoLoadFullGuildData)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await LoadCompleteGuildDataAsync(evt.Guild.Id, evt.Channels, evt.Members);
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Warning, $"Failed to load complete guild data for {evt.Guild.Id}: {ex.Message}");
                }
            });
        }
    }

    private void OnGuildUpdate(object? sender, DiscordGuild g)
    {
        _cache.UpsertGuild(g);
        DiscordEvents.RaiseGuildUpdated(this, new GuildEvent { Guild = g });
    }

    private void OnGuildDelete(object? sender, ulong gid)
    {
        _cache.RemoveGuild(gid);
        DiscordEvents.RaiseGuildRemoved(this, gid.ToString());
    }

    private void OnGuildEmojisUpdate(object? sender, GuildEmojisUpdateEvent e)
    {
        _cache.SetEmojis(e.GuildId, e.Emojis);
        DiscordEvents.RaiseGuildEmojisUpdated(this, e);
    }

    private void OnVoiceStateUpdate(object? sender, VoiceStateUpdateEvent e)
    {
        DiscordEvents.RaiseVoiceStateUpdated(this, e);
    }

    private void OnPresenceUpdate(object? sender, PresenceUpdateEvent e)
    {
        DiscordEvents.RaisePresenceUpdated(this, e);
    }

    private void OnTypingStart(object? sender, TypingStartEvent e)
    {
        DiscordEvents.RaiseTypingStarted(this, e);
    }

    private void OnWebhooksUpdate(object? sender, WebhooksUpdateEvent e)
    {
        DiscordEvents.RaiseWebhooksUpdated(this, e);
    }

    private void OnInviteCreate(object? sender, InviteCreateEvent e)
    {
        DiscordEvents.RaiseInviteCreated(this, e);
    }

    private void OnInviteDelete(object? sender, InviteDeleteEvent e)
    {
        DiscordEvents.RaiseInviteDeleted(this, e);
    }

    private void OnGuildIntegrationsUpdate(object? sender, GuildIntegrationsUpdateEvent e)
    {
        DiscordEvents.RaiseGuildIntegrationsUpdated(this, e);
    }

    private void OnChannelCreate(object? sender, DiscordChannel ch)
    {
        ulong? gid = ch.Guild_Id;
        if (gid is null)
        {
            _logger.Log(LogLevel.Information, $"DM channel created (id={ch.Id}, type={ch.Type}).");
            return; // do not cache DM channels
        }
        _cache.UpsertChannel(gid.Value, ch);
        DiscordGuild guild = _cache.TryGetGuild(gid.Value, out DiscordGuild g) ? g : new DiscordGuild { Id = gid.Value, Name = string.Empty };
        DiscordEvents.RaiseChannelCreated(this, new ChannelEvent { Channel = ch, Guild = guild });
    }

    private void OnChannelUpdate(object? sender, DiscordChannel ch)
    {
        ulong? gid = ch.Guild_Id;
        if (gid is null)
        {
            _logger.Log(LogLevel.Information, $"DM channel updated (id={ch.Id}, type={ch.Type}).");
            return;
        }
        _cache.UpsertChannel(gid.Value, ch);
        DiscordGuild guild = _cache.TryGetGuild(gid.Value, out DiscordGuild g) ? g : new DiscordGuild { Id = gid.Value, Name = string.Empty };
        DiscordEvents.RaiseChannelUpdated(this, new ChannelEvent { Channel = ch, Guild = guild });
    }

    private void OnChannelDelete(object? sender, DiscordChannel ch)
    {
        ulong? gid = ch.Guild_Id;
        if (gid is null)
        {
            _logger.Log(LogLevel.Information, $"DM channel deleted (id={ch.Id}, type={ch.Type}).");
            return;
        }
        _cache.RemoveChannel(gid.Value, ch.Id);
        DiscordGuild guild = _cache.TryGetGuild(gid.Value, out DiscordGuild g) ? g : new DiscordGuild { Id = gid.Value, Name = string.Empty };
        DiscordEvents.RaiseChannelDeleted(this, new ChannelEvent { Channel = ch, Guild = guild });
    }

    private void OnGuildRoleCreate(object? sender, GatewayRoleEvent e)
    {
        _cache.UpsertRole(e.GuildId, e.Role);
        DiscordGuild guild = _cache.TryGetGuild(e.GuildId, out DiscordGuild g) ? g : new DiscordGuild { Id = e.GuildId, Name = string.Empty };
        DiscordEvents.RaiseRoleCreated(this, new RoleEvent { Role = e.Role, Guild = guild });
    }

    private void OnGuildRoleUpdate(object? sender, GatewayRoleEvent e)
    {
        _cache.UpsertRole(e.GuildId, e.Role);
        DiscordGuild guild = _cache.TryGetGuild(e.GuildId, out DiscordGuild g) ? g : new DiscordGuild { Id = e.GuildId, Name = string.Empty };
        DiscordEvents.RaiseRoleUpdated(this, new RoleEvent { Role = e.Role, Guild = guild });
    }

    private void OnGuildRoleDelete(object? sender, GatewayRoleEvent e)
    {
        _cache.RemoveRole(e.GuildId, e.Role.Id);
        DiscordGuild guild = _cache.TryGetGuild(e.GuildId, out DiscordGuild g) ? g : new DiscordGuild { Id = e.GuildId, Name = string.Empty };
        DiscordEvents.RaiseRoleDeleted(this, new RoleEvent { Role = e.Role, Guild = guild });
    }

    private void OnThreadCreate(object? sender, DiscordChannel thread)
    {
        ulong? gid = thread.Guild_Id;
        if (gid is null) return;
        DiscordGuild guild = _cache.TryGetGuild(gid.Value, out DiscordGuild g) ? g : new DiscordGuild { Id = gid.Value, Name = string.Empty };
        DiscordEvents.RaiseThreadCreated(this, new ThreadEvent { Thread = thread, Guild = guild });
    }

    private void OnThreadUpdate(object? sender, DiscordChannel thread)
    {
        ulong? gid = thread.Guild_Id;
        if (gid is null) return;
        DiscordGuild guild = _cache.TryGetGuild(gid.Value, out DiscordGuild g) ? g : new DiscordGuild { Id = gid.Value, Name = string.Empty };
        DiscordEvents.RaiseThreadUpdated(this, new ThreadEvent { Thread = thread, Guild = guild });
    }

    private void OnThreadDelete(object? sender, DiscordChannel thread)
    {
        ulong? gid = thread.Guild_Id;
        if (gid is null) return;
        DiscordGuild guild = _cache.TryGetGuild(gid.Value, out DiscordGuild g) ? g : new DiscordGuild { Id = gid.Value, Name = string.Empty };
        DiscordEvents.RaiseThreadDeleted(this, new ThreadEvent { Thread = thread, Guild = guild });
    }

    private void OnGuildMemberAdd(object? sender, GatewayMemberEvent e)
    {
        _cache.UpsertMember(e.GuildId, e.Member);
        DiscordGuild guild = _cache.TryGetGuild(e.GuildId, out DiscordGuild g) ? g : new DiscordGuild { Id = e.GuildId, Name = string.Empty };
        DiscordEvents.RaiseMemberJoined(this, new MemberEvent { Member = e.Member, User = e.Member.User, Guild = guild });
    }

    private void OnGuildMemberUpdate(object? sender, GatewayMemberEvent e)
    {
        _cache.UpsertMember(e.GuildId, e.Member);
        DiscordGuild guild = _cache.TryGetGuild(e.GuildId, out DiscordGuild g) ? g : new DiscordGuild { Id = e.GuildId, Name = string.Empty };
        DiscordEvents.RaiseMemberUpdated(this, new MemberEvent { Member = e.Member, User = e.Member.User, Guild = guild });
    }

    private void OnGuildMemberRemove(object? sender, GatewayMemberEvent e)
    {
        _cache.RemoveMember(e.GuildId, e.Member.User.Id);
        DiscordGuild guild = _cache.TryGetGuild(e.GuildId, out DiscordGuild g) ? g : new DiscordGuild { Id = e.GuildId, Name = string.Empty };
        DiscordEvents.RaiseMemberLeft(this, new MemberEvent { Member = e.Member, User = e.Member.User, Guild = guild });
    }

    private void OnGuildMembersChunk(object? sender, GuildMembersChunkEvent e)
    {
        // Raise public event
        DiscordEvents.RaiseGuildMembersChunk(this, e);

        // Accumulate members from all chunks
        foreach (DiscordMember member in e.Members)
        {
            _cache.UpsertMember(e.GuildId, member);
        }

        _logger.Log(LogLevel.Debug, $"Received member chunk {e.ChunkIndex + 1}/{e.ChunkCount} for guild {e.GuildId} ({e.Members.Length} members)");

        // Check if this was the last chunk
        if (e.ChunkIndex == e.ChunkCount - 1 && _pendingMemberChunks.TryRemove(e.GuildId, out int _) && _cache.TryGetGuild(e.GuildId, out DiscordGuild guild))
        {
            _logger.Log(LogLevel.Information, $"Guild {guild.Name} ({e.GuildId}) is fully loaded");
            DiscordEvents.RaiseGuildReady(this, new GuildEvent { Guild = guild });
        }
    }

    private void OnGuildBanAdd(object? sender, GatewayUserEvent e)
    {
        _cache.RemoveMember(e.GuildId, e.User.Id);
        DiscordGuild guild = _cache.TryGetGuild(e.GuildId, out DiscordGuild g) ? g : new DiscordGuild { Id = e.GuildId, Name = string.Empty };
        DiscordEvents.RaiseBanAdded(this, new BanEvent { User = e.User, Guild = guild, Member = null });
    }

    private void OnGuildBanRemove(object? sender, GatewayUserEvent e)
    {
        DiscordGuild guild = _cache.TryGetGuild(e.GuildId, out DiscordGuild g) ? g : new DiscordGuild { Id = e.GuildId, Name = string.Empty };
        DiscordEvents.RaiseBanRemoved(this, new BanEvent { User = e.User, Guild = guild, Member = null });
    }

    private void OnUserUpdate(object? sender, DiscordUser u)
    {
        _botUser = u; // Store bot's own user
        DiscordEvents.RaiseBotUserUpdated(this, new BotUserEvent { User = u });
    }

    private void OnGuildAuditLogEntryCreate(object? sender, GatewayAuditLogEvent e)
    {
        DiscordGuild guild = _cache.TryGetGuild(e.GuildId, out DiscordGuild g) ? g : new DiscordGuild { Id = e.GuildId, Name = string.Empty };
        DiscordUser? user = e.Entry.UserId.HasValue && _cache.TryGetUser(e.Entry.UserId.Value, out DiscordUser u) ? u : null;
        DiscordUser? targetUser = e.Entry.TargetId.HasValue && _cache.TryGetUser(e.Entry.TargetId.Value, out DiscordUser tu) ? tu : null;

        AuditLogEvent evt = new()
        {
            Entry = e.Entry,
            Guild = guild,
            User = user,
            TargetUser = targetUser
        };
        DiscordEvents.RaiseAuditLogEntryCreated(this, evt);
    }

    private void OnMessageUpdate(object? sender, MessageUpdateEvent e) => DiscordEvents.RaiseMessageUpdated(this, e);
    private void OnMessageDelete(object? sender, MessageEvent e) => DiscordEvents.RaiseMessageDeleted(this, e);
    private void OnMessageDeleteBulk(object? sender, MessageEvent e) => DiscordEvents.RaiseMessagesBulkDeleted(this, e);
    private void OnMessageReactionAdd(object? sender, ReactionEvent e) => DiscordEvents.RaiseReactionAdded(this, e);
    private void OnMessageReactionRemove(object? sender, ReactionEvent e) => DiscordEvents.RaiseReactionRemoved(this, e);
    private void OnMessageReactionRemoveAll(object? sender, MessageEvent e) => DiscordEvents.RaiseReactionsCleared(this, e);
    private void OnMessageReactionRemoveEmoji(object? sender, ReactionEvent e) => DiscordEvents.RaiseReactionsClearedForEmoji(this, e);

    private void WireGatewayEvents()
    {
        // Forward logger to static event hub
        _logger.Logged += (_, msg) => DiscordEvents.RaiseLog(this, msg);

        // Only wire single gateway (sharded gateways are wired in StartAsync)
        if (_gateway == null) return;

        // Basic lifecycle and message routing
        _gateway.Connected += OnConnected;
        _gateway.Disconnected += OnDisconnected;
        _gateway.Error += OnError;

        // Message events
        _gateway.MessageCreate += (_, rawMsg) =>
        {
            try
            {
                // Parse IDs
                if (!ulong.TryParse(rawMsg.Id, out ulong messageId)) return;
                if (!ulong.TryParse(rawMsg.ChannelId, out ulong channelId)) return;
                ulong authorId = rawMsg.Author.Id;

                // Resolve entities from cache using DiscordContext
                DiscordChannel? channel = Context.DiscordContext.GetChannel(channelId);
                DiscordGuild? guild = rawMsg.GuildId != null && ulong.TryParse(rawMsg.GuildId, out ulong guildId)
                    ? Context.DiscordContext.GetGuild(guildId)
                    : null;

                // Try to get author from members cache first (has more info)
                DiscordUser? author = null;
                if (guild != null)
                {
                    var member = Context.DiscordContext.GetMember(authorId, guild.Id);
                    author = member?.User;
                }

                // If not in cache, create a minimal user object from the raw data
                if (author == null)
                {
                    author = new DiscordUser
                    {
                        Id = authorId,
                        Username = rawMsg.Author.Username,
                        Guilds = []
                    };
                }

                // Channel is required - if we don't have it, skip the event
                if (channel == null) return;

                // Create enriched event
                MessageCreateEvent evt = new()
                {
                    Id = messageId,
                    Channel = channel,
                    Guild = guild,
                    Content = rawMsg.Content,
                    Author = author
                };

                // Raise MessageCreated for all messages (guild and DM)
                DiscordEvents.RaiseMessageCreated(this, evt);

                // Also raise DirectMessageReceived for DMs
                if (rawMsg.GuildId is null)
                {
                    CommandContext ctx = new(rawMsg.ChannelId, rawMsg, _rest);
                    DiscordEvents.RaiseDirectMessageReceived(this, new DirectMessageEvent { Message = rawMsg, Context = ctx });
                }
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Warning, $"Failed to process MESSAGE_CREATE event: {ex.Message}");
            }
        };

        // Slash interactions
        _gateway.InteractionCreate += OnInteractionCreate;

        // Guild events
        _gateway.GuildCreate += OnGuildCreate;
        _gateway.GuildUpdate += OnGuildUpdate;
        _gateway.GuildDelete += OnGuildDelete;
        _gateway.GuildEmojisUpdate += OnGuildEmojisUpdate;

        // Other events
        _gateway.VoiceStateUpdate += OnVoiceStateUpdate;
        _gateway.PresenceUpdate += OnPresenceUpdate;
        _gateway.TypingStart += OnTypingStart;
        _gateway.WebhooksUpdate += OnWebhooksUpdate;
        _gateway.InviteCreate += OnInviteCreate;
        _gateway.InviteDelete += OnInviteDelete;
        _gateway.GuildIntegrationsUpdate += OnGuildIntegrationsUpdate;

        // Channel events
        _gateway.ChannelCreate += OnChannelCreate;
        _gateway.ChannelUpdate += OnChannelUpdate;
        _gateway.ChannelDelete += OnChannelDelete;

        // Role events
        _gateway.GuildRoleCreate += OnGuildRoleCreate;
        _gateway.GuildRoleUpdate += OnGuildRoleUpdate;
        _gateway.GuildRoleDelete += OnGuildRoleDelete;

        // Thread events
        _gateway.ThreadCreate += OnThreadCreate;
        _gateway.ThreadUpdate += OnThreadUpdate;
        _gateway.ThreadDelete += OnThreadDelete;

        // Member events
        _gateway.GuildMemberAdd += OnGuildMemberAdd;
        _gateway.GuildMemberUpdate += OnGuildMemberUpdate;
        _gateway.GuildMemberRemove += OnGuildMemberRemove;
        _gateway.GuildMembersChunk += OnGuildMembersChunk;

        // Ban events
        _gateway.GuildBanAdd += OnGuildBanAdd;
        _gateway.GuildBanRemove += OnGuildBanRemove;

        // Bot user change
        _gateway.UserUpdate += OnUserUpdate;

        // Audit log events
        _gateway.GuildAuditLogEntryCreate += OnGuildAuditLogEntryCreate;

        // Message events
        _gateway.MessageUpdate += OnMessageUpdate;
        _gateway.MessageDelete += OnMessageDelete;
        _gateway.MessageDeleteBulk += OnMessageDeleteBulk;

        // Reaction events
        _gateway.MessageReactionAdd += OnMessageReactionAdd;
        _gateway.MessageReactionRemove += OnMessageReactionRemove;
        _gateway.MessageReactionRemoveAll += OnMessageReactionRemoveAll;
        _gateway.MessageReactionRemoveEmoji += OnMessageReactionRemoveEmoji;
    }

    /// <summary>
    /// Disposes managed resources asynchronously.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _gateway?.Dispose();
        _shardManager?.Dispose();
        _coordinator?.Dispose();
        _worker?.Dispose();
        _rest.Dispose();
        _cts.Dispose();
        DiscordContext.ClearProvider();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Disposes of managed resources. Prefer <see cref="DisposeAsync"/> when possible.
    /// </summary>
    public void Dispose()
    {
        _ = DisposeAsync();
    }

    /// <summary>
    /// Create a new builder instance for configuring the bot.
    /// </summary>
    public static Builder NewBuilder() => new();

    /// <summary>
    /// Factory method for DI scenarios to create a bot instance from <see cref="DiscordBotOptions"/>.
    /// No external DI packages are required; you can register this factory with your container of choice.
    /// </summary>
    public static DiscordBot FromOptions(DiscordBotOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Token)) throw new InvalidOperationException("Token is required");

        DiscordBot bot = new(
            options.Token,
            options.Intents,
            options.JsonOptions,
            new NativeLogger(options.MinimumLogLevel, options.LogSink),
            options.TimeProvider,
            options.PreloadGuilds,
            options.PreloadChannels,
            options.PreloadMembers,
            options.AutoLoadFullGuildData,
            options.DevelopmentMode,
            options.DevelopmentGuildIds,
            options.ShardMode,
            options.ShardId,
            options.TotalShards,
            options.CoordinatorUrl,
            options.WorkerListenUrl,
            options.WorkerId,
            options.IsOriginalCoordinator);

        return bot;
    }

    public sealed class Builder
    {
        private string? _token;
        private DiscordIntents _intents = DiscordIntents.Guilds | DiscordIntents.GuildMessages | DiscordIntents.DirectMessages | DiscordIntents.MessageContent;
        private JsonSerializerOptions _json = new(Serialization.DiscordJsonContext.Default.Options)
        {
            // Preserve prior behavior on top of source-generated options
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
            TypeInfoResolver = Serialization.DiscordJsonContext.Default
        };
        private NativeLogger _logger = new();
        private TimeProvider? _timeProvider;
        private bool _preloadGuilds;
        private bool _preloadChannels;
        private bool _preloadMembers;
        private bool _autoLoadFullGuildData = true; // Default enabled
        private bool _developmentMode;
        private readonly List<string> _developmentGuildIds = [];
        private ShardMode _shardMode = ShardMode.SingleProcess;
        private int? _shardId;
        private int? _totalShards;
        private string? _coordinatorUrl;
        private string? _workerListenUrl;
        private string? _workerId;
        private bool _isOriginalCoordinator;

        /// <summary>
        /// Sets the bot token used for authentication.
        /// </summary>
        public Builder WithToken(string token)
        {
            _token = token ?? throw new ArgumentNullException(nameof(token));
            return this;
        }

        /// <summary>
        /// Sets the Discord gateway intents for the bot.
        /// </summary>
        public Builder WithIntents(DiscordIntents intents)
        {
            _intents = intents;
            return this;
        }

        /// <summary>
        /// Sets the logger instance used by the bot.
        /// </summary>
        public Builder WithLogger(NativeLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            return this;
        }

        /// <summary>
        /// Sets JSON serialization options used for REST and gateway payloads.
        /// </summary>
        public Builder WithJsonOptions(JsonSerializerOptions options)
        {
            _json = options ?? throw new ArgumentNullException(nameof(options));
            return this;
        }

        /// <summary>
        /// Sets the time provider used for timing and rate limiting operations.
        /// </summary>
        public Builder WithTimeProvider(TimeProvider provider)
        {
            _timeProvider = provider ?? throw new ArgumentNullException(nameof(provider));
            return this;
        }

        /// <summary>
        /// Preload caches on Start using REST. Members require the GuildMembers intent and can be heavy.
        /// </summary>
        public Builder WithPreloadOnStart(bool guilds = true, bool channels = true, bool members = false)
        {
            _preloadGuilds = guilds;
            _preloadChannels = channels;
            _preloadMembers = members;
            return this;
        }

        /// <summary>
        /// Enables or disables development mode. When enabled, all registered slash commands
        /// are synchronized immediately to the configured development guilds on Start.
        /// </summary>
        public Builder WithDevelopmentMode(bool enabled = true)
        {
            _developmentMode = enabled;
            return this;
        }

        /// <summary>
        /// Adds a single development guild id for immediate slash command synchronization.
        /// </summary>
        public Builder WithDevelopmentGuild(string guildId)
        {
            if (!string.IsNullOrWhiteSpace(guildId))
                _developmentGuildIds.Add(guildId.Trim());
            return this;
        }

        /// <summary>
        /// Adds multiple development guild ids for immediate slash command synchronization.
        /// </summary>
        public Builder WithDevelopmentGuilds(IEnumerable<string> guildIds)
        {
            ArgumentNullException.ThrowIfNull(guildIds);
            foreach (string id in guildIds)
            {
                if (!string.IsNullOrWhiteSpace(id)) _developmentGuildIds.Add(id.Trim());
            }
            return this;
        }

        /// <summary>
        /// Controls whether the bot automatically loads complete guild data after GUILD_CREATE.
        /// When enabled (default), fires GuildReady event when guild is fully loaded with all members, channels, roles, etc.
        /// </summary>
        public Builder WithAutoLoadFullGuildData(bool enabled = true)
        {
            _autoLoadFullGuildData = enabled;
            return this;
        }

        /// <summary>
        /// Configures single-process sharding with explicit shard ID and total.
        /// Example: builder.WithSharding(shardId: 0, totalShards: 4)
        /// </summary>
        public Builder WithSharding(int shardId, int totalShards)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(shardId);
            ArgumentOutOfRangeException.ThrowIfLessThan(totalShards, 1);
            if (shardId >= totalShards) throw new ArgumentException($"ShardId {shardId} must be less than TotalShards {totalShards}");

            _shardMode = ShardMode.SingleProcess;
            _shardId = shardId;
            _totalShards = totalShards;
            return this;
        }

        /// <summary>
        /// Configures distributed sharding as a coordinator.
        /// Example: builder.WithDistributedCoordinator(listenUrl: "http://+:8080/", isOriginal: true)
        /// </summary>
        public Builder WithDistributedCoordinator(string listenUrl, bool isOriginalCoordinator = true)
        {
            if (string.IsNullOrWhiteSpace(listenUrl)) throw new ArgumentNullException(nameof(listenUrl));

            _shardMode = ShardMode.Distributed;
            _workerListenUrl = listenUrl;
            _isOriginalCoordinator = isOriginalCoordinator;
            return this;
        }

        /// <summary>
        /// Configures distributed sharding as a worker node.
        /// Example: builder.WithDistributedWorker(coordinatorUrl: "http://192.168.1.100:8080/", listenUrl: "http://+:8080/", workerId: "worker-1")
        /// </summary>
        public Builder WithDistributedWorker(string coordinatorUrl, string listenUrl, string? workerId = null)
        {
            if (string.IsNullOrWhiteSpace(coordinatorUrl)) throw new ArgumentNullException(nameof(coordinatorUrl));
            if (string.IsNullOrWhiteSpace(listenUrl)) throw new ArgumentNullException(nameof(listenUrl));

            _shardMode = ShardMode.Distributed;
            _coordinatorUrl = coordinatorUrl;
            _workerListenUrl = listenUrl;
            _workerId = workerId ?? $"{Environment.MachineName}-{Guid.NewGuid():N}";
            return this;
        }

        /// <summary>
        /// Builds a configured <see cref="DiscordBot"/> instance.
        /// </summary>
        public DiscordBot Build()
        {
            if (string.IsNullOrWhiteSpace(_token))
                throw new InvalidOperationException("Token is required");

            DiscordBot bot = new(_token!, _intents, _json, _logger, _timeProvider, _preloadGuilds, _preloadChannels, _preloadMembers, _autoLoadFullGuildData, _developmentMode, _developmentGuildIds,
                _shardMode, _shardId, _totalShards, _coordinatorUrl, _workerListenUrl, _workerId, _isOriginalCoordinator);

            // Auto-register any manifests provided by source-generated initializers
            foreach (IGeneratedManifestProvider provider in GeneratedRegistry.Providers)
            {
                try
                {
                    IGeneratedManifest manifest = provider.CreateManifest();
                    bot.RegisterGeneratedHandlers(manifest);
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Warning, $"Failed to register generated manifest: {ex.Message}");
                }
            }

            return bot;
        }
    }

    /// <summary>
    /// Registers generated handlers and definitions emitted by the source generator.
    /// </summary>
    public void RegisterGeneratedHandlers(IGeneratedManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        _generatedManifests.Add(manifest);
        _slashCommands.RegisterGeneratedManifest(manifest);
        foreach (ComponentHandler ch in manifest.Components)
            _components.RegisterGenerated(ch);
    }

    private async Task PreloadAsync(CancellationToken ct)
    {
        try
        {
            if (_preloadGuilds || _preloadChannels || _preloadMembers)
            {
                DiscordGuild[] guilds = await _rest.GetAsync<DiscordGuild[]>("/users/@me/guilds", ct).ConfigureAwait(false) ?? [];
                _cache.ReplaceGuilds(guilds);

                if (_preloadChannels)
                {
                    foreach (DiscordGuild g in guilds)
                    {
                        var ch = await GetGuildChannelsAsync(g.Id.ToString(), ct).ConfigureAwait(false);
                        _cache.SetChannels(g.Id, ch);
                    }
                }

                if (_preloadMembers)
                {
                    foreach (DiscordGuild g in guilds)
                    {
                        List<DiscordMember> all = new(1024);
                        string? after = null;
                        while (true)
                        {
                            var page = (await ListGuildMembersAsync(g.Id.ToString(), 1000, after, ct).ConfigureAwait(false)).ToArray();
                            if (page.Length == 0) break;
                            all.AddRange(page);
                            after = page[^1].User.Id.ToString();
                            if (page.Length < 1000) break;
                        }
                        _cache.SetMembers(g.Id, all);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Cache preload failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads complete guild data after GUILD_CREATE, ensuring all channels and members are cached.
    /// Fires GuildReady event when complete.
    /// </summary>
    private async Task LoadCompleteGuildDataAsync(ulong guildId, DiscordChannel[]? guildCreateChannels, DiscordMember[]? guildCreateMembers)
    {
        await Task.Delay(100); // Small delay to let connection stabilize

        bool needsMembers = false;
        bool needsChannels = false;

        // Check if we need to load channels via REST (if not provided in GUILD_CREATE)
        if (guildCreateChannels is null || guildCreateChannels.Length == 0)
        {
            _logger.Log(LogLevel.Debug, $"Loading channels for guild {guildId} via REST");
            try
            {
                var channels = await GetGuildChannelsAsync(guildId.ToString(), _cts.Token).ConfigureAwait(false);
                _cache.SetChannels(guildId, channels);
                needsChannels = true;
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Warning, $"Failed to load channels for guild {guildId}: {ex.Message}");
            }
        }

        // Check if we need to load full member list via gateway chunking
        if ((_intents & DiscordIntents.GuildMembers) == DiscordIntents.GuildMembers)
        {
            _logger.Log(LogLevel.Debug, $"Requesting full member list for guild {guildId}");
            try
            {
                // Register that we're waiting for member chunks
                _pendingMemberChunks.TryAdd(guildId, 1);
                await _gateway!.RequestGuildMembersAsync(guildId.ToString());
                needsMembers = true;
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Warning, $"Failed to request members for guild {guildId}: {ex.Message}");
                _pendingMemberChunks.TryRemove(guildId, out _);
            }
        }

        // If we didn't need to load anything asynchronously, fire GuildReady immediately
        if (!needsMembers && !needsChannels)
        {
            if (_cache.TryGetGuild(guildId, out DiscordGuild guild))
            {
                _logger.Log(LogLevel.Information, $"Guild {guild.Name} ({guildId}) is fully loaded");
                DiscordEvents.RaiseGuildReady(this, new GuildEvent { Guild = guild });
            }
        }
        else if (!needsMembers)
        {
            // We loaded channels but don't need members (no GuildMembers intent)
            // Fire GuildReady after a small delay to ensure channels are cached
            await Task.Delay(50);
            if (_cache.TryGetGuild(guildId, out DiscordGuild guild))
            {
                _logger.Log(LogLevel.Information, $"Guild {guild.Name} ({guildId}) is fully loaded");
                DiscordEvents.RaiseGuildReady(this, new GuildEvent { Guild = guild });
            }
        }
        // If we need members, GuildReady will fire when the last chunk arrives (handled in GuildMembersChunk event)
    }
}
