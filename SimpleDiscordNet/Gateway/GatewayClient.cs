using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Text.Json;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Logging;
using SimpleDiscordNet.Models;
using SimpleDiscordNet.Events;

namespace SimpleDiscordNet.Gateway;

[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "GatewayClient uses JsonSerializerOptions configured with source-generated DiscordJsonContext for all gateway payload types.")]
[UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "GatewayClient uses JsonSerializerOptions configured with source-generated DiscordJsonContext for all gateway payload types.")]
internal sealed partial class GatewayClient(string token, DiscordIntents intents, JsonSerializerOptions json, int? shardId = null, int? totalShards = null)
    : IDisposable
{
    private ClientWebSocket _ws = new();
    private readonly CancellationTokenSource _internalCts = new();
    private Task? _loopTask;
    private long _seq;
    private string? _sessionId;
    private int _heartbeatIntervalMs;
    private Timer? _heartbeatTimer;
    private volatile bool _awaitingHeartbeatAck;
    private int _missedHeartbeatAcks;
    private readonly Random _rand = new();
    private int _reconnectAttempt;
    private volatile int _reconnecting; // 0 = no, 1 = yes
    private volatile bool _autoReconnect = true;

    internal int? ShardId { get; } = shardId;
    internal int? TotalShards { get; } = totalShards;

    public event EventHandler? Connected;
    public event EventHandler<Exception?>? Disconnected;
    public event EventHandler<Exception>? Error;
    public event EventHandler<MessageCreateEventRaw>? MessageCreate;
    public event EventHandler<InteractionCreateEvent>? InteractionCreate;

    // Domain events (internal) to be surfaced by DiscordBot
    public event EventHandler<GuildCreateEvent>? GuildCreate;
    public event EventHandler<DiscordGuild>? GuildUpdate;
    public event EventHandler<ulong>? GuildDelete; // guild id
    public event EventHandler<GuildEmojisUpdateEvent>? GuildEmojisUpdate;
    #pragma warning disable CS0067
    [SuppressMessage("CodeQuality", "CS0067", Justification = "Event is exposed for external subscribers")]
    public event EventHandler<VoiceStateUpdateEvent>? VoiceStateUpdate;
    [SuppressMessage("CodeQuality", "CS0067", Justification = "Event is exposed for external subscribers")]
    public event EventHandler<PresenceUpdateEvent>? PresenceUpdate;
    [SuppressMessage("CodeQuality", "CS0067", Justification = "Event is exposed for external subscribers")]
    public event EventHandler<TypingStartEvent>? TypingStart;
    [SuppressMessage("CodeQuality", "CS0067", Justification = "Event is exposed for external subscribers")]
    public event EventHandler<WebhooksUpdateEvent>? WebhooksUpdate;
    [SuppressMessage("CodeQuality", "CS0067", Justification = "Event is exposed for external subscribers")]
    public event EventHandler<InviteCreateEvent>? InviteCreate;
    [SuppressMessage("CodeQuality", "CS0067", Justification = "Event is exposed for external subscribers")]
    public event EventHandler<InviteDeleteEvent>? InviteDelete;
    [SuppressMessage("CodeQuality", "CS0067", Justification = "Event is exposed for external subscribers")]
    public event EventHandler<GuildIntegrationsUpdateEvent>? GuildIntegrationsUpdate;

    [SuppressMessage("CodeQuality", "CS0067", Justification = "Event is exposed for external subscribers")]
    public event EventHandler<AutoModerationRuleCreatedEvent>? AutoModerationRuleCreated;
    [SuppressMessage("CodeQuality", "CS0067", Justification = "Event is exposed for external subscribers")]
    public event EventHandler<AutoModerationRuleUpdatedEvent>? AutoModerationRuleUpdated;
    [SuppressMessage("CodeQuality", "CS0067", Justification = "Event is exposed for external subscribers")]
    public event EventHandler<AutoModerationRuleDeletedEvent>? AutoModerationRuleDeleted;
    [SuppressMessage("CodeQuality", "CS0067", Justification = "Event is exposed for external subscribers")]
    public event EventHandler<StageInstanceCreatedEvent>? StageInstanceCreated;
    [SuppressMessage("CodeQuality", "CS0067", Justification = "Event is exposed for external subscribers")]
    public event EventHandler<StageInstanceUpdatedEvent>? StageInstanceUpdated;
    [SuppressMessage("CodeQuality", "CS0067", Justification = "Event is exposed for external subscribers")]
    public event EventHandler<StageInstanceDeletedEvent>? StageInstanceDeleted;
    [SuppressMessage("CodeQuality", "CS0067", Justification = "Event is exposed for external subscribers")]
    public event EventHandler<GuildScheduledEventCreatedEvent>? GuildScheduledEventCreated;
    [SuppressMessage("CodeQuality", "CS0067", Justification = "Event is exposed for external subscribers")]
    public event EventHandler<GuildScheduledEventUpdatedEvent>? GuildScheduledEventUpdated;
    [SuppressMessage("CodeQuality", "CS0067", Justification = "Event is exposed for external subscribers")]
    public event EventHandler<GuildScheduledEventDeletedEvent>? GuildScheduledEventDeleted;
    [SuppressMessage("CodeQuality", "CS0067", Justification = "Event is exposed for external subscribers")]
    public event EventHandler<IntegrationCreatedEvent>? IntegrationCreated;
    [SuppressMessage("CodeQuality", "CS0067", Justification = "Event is exposed for external subscribers")]
    public event EventHandler<IntegrationUpdatedEvent>? IntegrationUpdated;
    [SuppressMessage("CodeQuality", "CS0067", Justification = "Event is exposed for external subscribers")]
    public event EventHandler<IntegrationDeletedEvent>? IntegrationDeleted;
    [SuppressMessage("CodeQuality", "CS0067", Justification = "Event is exposed for external subscribers")]
    public event EventHandler<VoiceServerUpdateEvent>? VoiceServerUpdate;
    [SuppressMessage("CodeQuality", "CS0067", Justification = "Event is exposed for external subscribers")]
    public event EventHandler<GuildJoinRequestCreatedEvent>? GuildJoinRequestCreated;
    [SuppressMessage("CodeQuality", "CS0067", Justification = "Event is exposed for external subscribers")]
    public event EventHandler<GuildJoinRequestUpdatedEvent>? GuildJoinRequestUpdated;
    [SuppressMessage("CodeQuality", "CS0067", Justification = "Event is exposed for external subscribers")]
    public event EventHandler<GuildJoinRequestDeletedEvent>? GuildJoinRequestDeleted;
    [SuppressMessage("CodeQuality", "CS0067", Justification = "Event is exposed for external subscribers")]
    public event EventHandler<PollVoteAddedEvent>? PollVoteAdded;
    [SuppressMessage("CodeQuality", "CS0067", Justification = "Event is exposed for external subscribers")]
    public event EventHandler<PollVoteRemovedEvent>? PollVoteRemoved;
    #pragma warning restore CS0067

    public event EventHandler<DiscordChannel>? ChannelCreate;
    public event EventHandler<DiscordChannel>? ChannelUpdate;
    public event EventHandler<DiscordChannel>? ChannelDelete;

    public event EventHandler<GatewayRoleEvent>? GuildRoleCreate;
    public event EventHandler<GatewayRoleEvent>? GuildRoleUpdate;
    public event EventHandler<GatewayRoleEvent>? GuildRoleDelete;

    public event EventHandler<DiscordChannel>? ThreadCreate;
    public event EventHandler<DiscordChannel>? ThreadUpdate;
    public event EventHandler<DiscordChannel>? ThreadDelete;

    public event EventHandler<GatewayMemberEvent>? GuildMemberAdd;
    public event EventHandler<GatewayMemberEvent>? GuildMemberUpdate;
    public event EventHandler<GatewayMemberEvent>? GuildMemberRemove; // Member user-only on remove
    public event EventHandler<GuildMembersChunkEvent>? GuildMembersChunk;

    public event EventHandler<GatewayUserEvent>? GuildBanAdd;
    public event EventHandler<GatewayUserEvent>? GuildBanRemove;

    public event EventHandler<DiscordUser>? UserUpdate; // Bot user

    public event EventHandler<GatewayAuditLogEvent>? GuildAuditLogEntryCreate;

    public event EventHandler<MessageUpdateEvent>? MessageUpdate;
    public event EventHandler<MessageEvent>? MessageDelete;
    public event EventHandler<MessageEvent>? MessageDeleteBulk;

    public event EventHandler<ReactionEvent>? MessageReactionAdd;
    public event EventHandler<ReactionEvent>? MessageReactionRemove;
    public event EventHandler<MessageEvent>? MessageReactionRemoveAll;
    public event EventHandler<ReactionEvent>? MessageReactionRemoveEmoji;

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await ConnectSocketAsync(cancellationToken).ConfigureAwait(false);
        _loopTask = Task.Run(() => ReceiveLoop(_internalCts.Token), cancellationToken);
        Connected?.Invoke(this, EventArgs.Empty);
    }

    public async Task DisconnectAsync()
    {
        try
        {
            _autoReconnect = false;
            await _internalCts.CancelAsync();
            if (_ws.State == WebSocketState.Open)
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "shutdown", CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, ex);
        }
        finally
        {
            try { _heartbeatTimer?.Dispose(); } catch { /* ignored */ }

            Disconnected?.Invoke(this, null);
        }
    }

    private void HandleDispatch(string? eventName, JsonElement data)
    {
        if (string.Equals(eventName, "READY", StringComparison.Ordinal))
        {
            if (data.TryGetProperty("session_id", out JsonElement sid))
                _sessionId = sid.GetString();
            return;
        }

        if (string.Equals(eventName, "MESSAGE_CREATE", StringComparison.Ordinal))
        {
            try
            {
                string id = data.GetProperty("id").GetString()!;
                string channelId = data.GetProperty("channel_id").GetString()!;
                string content = data.GetProperty("content").GetString() ?? string.Empty;
                JsonElement authorObj = data.GetProperty("author");
                Author author = new() { Id = authorObj.GetProperty("id").GetUInt64(), Username = authorObj.GetProperty("username").GetString()! };
                string? guildId = null;
                if (data.TryGetProperty("guild_id", out JsonElement gidEl))
                {
                    guildId = gidEl.GetString();
                }

                MessageCreateEventRaw evt = new()
                {
                    Id = id,
                    ChannelId = channelId,
                    GuildId = guildId,
                    Content = content,
                    Author = author
                };
                MessageCreate?.Invoke(this, evt);
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, ex);
            }
        }

        // Guild events
        else if (string.Equals(eventName, "GUILD_CREATE", StringComparison.Ordinal))
        {
            try
            {
                ulong guildId = data.GetProperty("id").GetUInt64();

                // Parse roles
                // First create guild without roles
                DiscordGuild g = new()
                {
                    Id = guildId,
                    Name = data.GetProperty("name").GetString() ?? string.Empty
                };

                // Parse roles with guild reference
                DiscordRole[]? roles = null;
                if (data.TryGetProperty("roles", out JsonElement rolesEl) && rolesEl.ValueKind == JsonValueKind.Array)
                {
                    List<DiscordRole> roleList = new();
                    foreach (JsonElement roleData in rolesEl.EnumerateArray())
                    {
                        DiscordRole role = new()
                        {
                            Id = roleData.GetProperty("id").GetUInt64(),
                            Name = roleData.GetProperty("name").GetString() ?? string.Empty,
                            Guild = g,
                            Color = roleData.TryGetProperty("color", out JsonElement c) ? c.GetInt32() : 0,
                            Position = roleData.TryGetProperty("position", out JsonElement p) ? p.GetInt32() : 0,
                            Permissions = roleData.TryGetProperty("permissions", out JsonElement perms) ? perms.GetUInt64() : 0UL
                        };
                        roleList.Add(role);
                    }
                    roles = roleList.ToArray();
                }

                // Parse emojis
                DiscordEmoji[]? emojis = null;
                if (data.TryGetProperty("emojis", out JsonElement emojisEl) && emojisEl.ValueKind == JsonValueKind.Array)
                {
                    List<DiscordEmoji> emojiList = new();
                    foreach (JsonElement emojiData in emojisEl.EnumerateArray())
                    {
                        DiscordEmoji emoji = new()
                        {
                            Id = emojiData.TryGetProperty("id", out JsonElement eid) ? eid.GetString() : null,
                            Name = emojiData.TryGetProperty("name", out JsonElement en) ? en.GetString() : null,
                            Animated = emojiData.TryGetProperty("animated", out JsonElement anim) && anim.ValueKind == JsonValueKind.True
                        };
                        emojiList.Add(emoji);
                    }
                    emojis = emojiList.ToArray();
                }

                // Set roles and emojis on guild
                g.Roles = roles;
                g.Emojis = emojis;

                // Parse channels
                DiscordChannel[]? channels = null;
                if (data.TryGetProperty("channels", out JsonElement channelsEl) && channelsEl.ValueKind == JsonValueKind.Array)
                {
                    List<DiscordChannel> channelList = new();
                    foreach (JsonElement channelData in channelsEl.EnumerateArray())
                    {
                        DiscordChannel channel = new()
                        {
                            Id = channelData.GetProperty("id").GetUInt64(),
                            Name = channelData.TryGetProperty("name", out JsonElement n) ? (n.GetString() ?? string.Empty) : string.Empty,
                            Type = channelData.TryGetProperty("type", out JsonElement t) ? t.GetInt32() : 0,
                            Parent_Id = channelData.TryGetProperty("parent_id", out JsonElement pid) && pid.ValueKind != JsonValueKind.Null ? pid.GetUInt64() : null,
                            Guild_Id = guildId
                        };
                        channelList.Add(channel);
                    }
                    channels = channelList.ToArray();
                }

                // Parse members
                DiscordMember[]? members = null;
                if (data.TryGetProperty("members", out JsonElement membersEl) && membersEl.ValueKind == JsonValueKind.Array)
                {
                    List<DiscordMember> memberList = [];
                    foreach (JsonElement memberData in membersEl.EnumerateArray())
                    {
                        if (memberData.TryGetProperty("user", out JsonElement userData))
                        {
                            DiscordUser user = ParseUser(userData);
                            ulong[] memberRoles = memberData.TryGetProperty("roles", out JsonElement rolesArr) && rolesArr.ValueKind == JsonValueKind.Array
                                ? rolesArr.EnumerateArray().Select(static x => x.GetUInt64()).ToArray()
                                : [];
                            string? nick = memberData.TryGetProperty("nick", out JsonElement nickEl) && nickEl.ValueKind != JsonValueKind.Null ? nickEl.GetString() : null;

                            DiscordMember member = new() { User = user, Guild = g, Nick = nick, Roles = memberRoles };
                            memberList.Add(member);
                        }
                    }
                    members = memberList.ToArray();
                }

                // Parse threads
                DiscordChannel[]? threads = null;
                if (data.TryGetProperty("threads", out JsonElement threadsEl) && threadsEl.ValueKind == JsonValueKind.Array)
                {
                    List<DiscordChannel> threadList = [];
                    foreach (JsonElement threadData in threadsEl.EnumerateArray())
                    {
                        DiscordChannel thread = new()
                        {
                            Id = threadData.GetProperty("id").GetUInt64(),
                            Name = threadData.TryGetProperty("name", out JsonElement tn) ? (tn.GetString() ?? string.Empty) : string.Empty,
                            Type = threadData.TryGetProperty("type", out JsonElement tt) ? tt.GetInt32() : 0,
                            Parent_Id = threadData.TryGetProperty("parent_id", out JsonElement tpid) && tpid.ValueKind != JsonValueKind.Null ? tpid.GetUInt64() : null,
                            Guild_Id = guildId
                        };
                        threadList.Add(thread);
                    }
                    threads = threadList.ToArray();
                }

                GuildCreateEvent evt = new()
                {
                    Guild = g,
                    Channels = channels,
                    Members = members,
                    Threads = threads
                };
                GuildCreate?.Invoke(this, evt);
            }
            catch (Exception ex) { Error?.Invoke(this, ex); }
        }
        else if (string.Equals(eventName, "GUILD_UPDATE", StringComparison.Ordinal))
        {
            try
            {
                DiscordGuild g = new()
                {
                    Id = data.GetProperty("id").GetUInt64(),
                    Name = data.GetProperty("name").GetString() ?? string.Empty
                };
                GuildUpdate?.Invoke(this, g);
            }
            catch (Exception ex) { Error?.Invoke(this, ex); }
        }
        else if (string.Equals(eventName, "GUILD_DELETE", StringComparison.Ordinal))
        {
            try
            {
                ulong gid = data.GetProperty("id").GetUInt64();
                GuildDelete?.Invoke(this, gid);
            }
            catch (Exception ex) { Error?.Invoke(this, ex); }
        }
        else if (string.Equals(eventName, "GUILD_EMOJIS_UPDATE", StringComparison.Ordinal))
        {
            try
            {
                ulong guildId = data.GetProperty("guild_id").GetUInt64();
                DiscordEmoji[]? emojis = null;
                if (data.TryGetProperty("emojis", out JsonElement emojisEl) && emojisEl.ValueKind == JsonValueKind.Array)
                {
                    List<DiscordEmoji> emojiList = [];
                    foreach (JsonElement emojiData in emojisEl.EnumerateArray())
                    {
                        DiscordEmoji emoji = new()
                        {
                            Id = emojiData.TryGetProperty("id", out JsonElement eid) ? eid.GetString() : null,
                            Name = emojiData.TryGetProperty("name", out JsonElement en) ? en.GetString() : null,
                            Animated = emojiData.TryGetProperty("animated", out JsonElement anim) && anim.ValueKind == JsonValueKind.True
                        };
                        emojiList.Add(emoji);
                    }
                    emojis = emojiList.ToArray();
                }
                GuildEmojisUpdate?.Invoke(this, new GuildEmojisUpdateEvent { GuildId = guildId, Emojis = emojis ?? [] });
            }
            catch (Exception ex) { Error?.Invoke(this, ex); }
        }

        // Channel events
        else if (string.Equals(eventName, "CHANNEL_CREATE", StringComparison.Ordinal))
        {
            TryEmitChannelEvent(data, ChannelCreate);
        }
        else if (string.Equals(eventName, "CHANNEL_UPDATE", StringComparison.Ordinal))
        {
            TryEmitChannelEvent(data, ChannelUpdate);
        }
        else if (string.Equals(eventName, "CHANNEL_DELETE", StringComparison.Ordinal))
        {
            TryEmitChannelEvent(data, ChannelDelete);
        }

        // Role events
        else if (string.Equals(eventName, "GUILD_ROLE_CREATE", StringComparison.Ordinal))
        {
            TryEmitRoleEvent(data, GuildRoleCreate);
        }
        else if (string.Equals(eventName, "GUILD_ROLE_UPDATE", StringComparison.Ordinal))
        {
            TryEmitRoleEvent(data, GuildRoleUpdate);
        }
        else if (string.Equals(eventName, "GUILD_ROLE_DELETE", StringComparison.Ordinal))
        {
            TryEmitRoleDeleteEvent(data, GuildRoleDelete);
        }

        // Thread events
        else if (string.Equals(eventName, "THREAD_CREATE", StringComparison.Ordinal))
        {
            TryEmitChannelEvent(data, ThreadCreate);
        }
        else if (string.Equals(eventName, "THREAD_UPDATE", StringComparison.Ordinal))
        {
            TryEmitChannelEvent(data, ThreadUpdate);
        }
        else if (string.Equals(eventName, "THREAD_DELETE", StringComparison.Ordinal))
        {
            TryEmitChannelEvent(data, ThreadDelete);
        }

        // Member events
        else if (string.Equals(eventName, "GUILD_MEMBER_ADD", StringComparison.Ordinal))
        {
            TryEmitMemberEvent(data, GuildMemberAdd);
        }
        else if (string.Equals(eventName, "GUILD_MEMBER_UPDATE", StringComparison.Ordinal))
        {
            TryEmitMemberEvent(data, GuildMemberUpdate);
        }
        else if (string.Equals(eventName, "GUILD_MEMBER_REMOVE", StringComparison.Ordinal))
        {
            TryEmitMemberRemoveEvent(data, GuildMemberRemove);
        }
        else if (string.Equals(eventName, "GUILD_MEMBERS_CHUNK", StringComparison.Ordinal))
        {
            try
            {
                ulong guildId = data.GetProperty("guild_id").GetUInt64();
                int chunkIndex = data.TryGetProperty("chunk_index", out JsonElement ci) ? ci.GetInt32() : 0;
                int chunkCount = data.TryGetProperty("chunk_count", out JsonElement cc) ? cc.GetInt32() : 1;

                // Create placeholder guild (will be replaced with actual guild in cache)
                DiscordGuild guild = new() { Id = guildId, Name = string.Empty };

                DiscordMember[]? members = null;
                if (data.TryGetProperty("members", out JsonElement membersEl) && membersEl.ValueKind == JsonValueKind.Array)
                {
                    List<DiscordMember> memberList = [];
                    foreach (JsonElement memberData in membersEl.EnumerateArray())
                    {
                        if (memberData.TryGetProperty("user", out JsonElement userData))
                        {
                            DiscordUser user = ParseUser(userData);
                            ulong[] memberRoles = memberData.TryGetProperty("roles", out JsonElement rolesArr) && rolesArr.ValueKind == JsonValueKind.Array
                                ? rolesArr.EnumerateArray().Select(static x => x.GetUInt64()).ToArray()
                                : [];
                            string? nick = memberData.TryGetProperty("nick", out JsonElement nickEl) && nickEl.ValueKind != JsonValueKind.Null ? nickEl.GetString() : null;

                            DiscordMember member = new() { User = user, Guild = guild, Nick = nick, Roles = memberRoles };
                            memberList.Add(member);
                        }
                    }
                    members = memberList.ToArray();
                }

                GuildMembersChunkEvent evt = new()
                {
                    GuildId = guildId,
                    Members = members ?? [],
                    ChunkIndex = chunkIndex,
                    ChunkCount = chunkCount
                };
                GuildMembersChunk?.Invoke(this, evt);
            }
            catch (Exception ex) { Error?.Invoke(this, ex); }
        }

        // Ban events
        else if (string.Equals(eventName, "GUILD_BAN_ADD", StringComparison.Ordinal))
        {
            TryEmitBanEvent(data, GuildBanAdd);
        }
        else if (string.Equals(eventName, "GUILD_BAN_REMOVE", StringComparison.Ordinal))
        {
            TryEmitBanEvent(data, GuildBanRemove);
        }

        // Bot user update
        else if (string.Equals(eventName, "USER_UPDATE", StringComparison.Ordinal))
        {
            try
            {
                DiscordUser u = ParseUser(data);
                UserUpdate?.Invoke(this, u);
            }
            catch (Exception ex) { Error?.Invoke(this, ex); }
        }

        // Audit log events
        else if (string.Equals(eventName, "GUILD_AUDIT_LOG_ENTRY_CREATE", StringComparison.Ordinal))
        {
            try
            {
                ulong guildId = data.GetProperty("guild_id").GetUInt64();
                DiscordAuditLogEntry entry = new()
                {
                    Id = data.GetProperty("id").GetUInt64(),
                    ActionType = data.GetProperty("action_type").GetInt32(),
                    TargetId = data.TryGetProperty("target_id", out JsonElement tid) && tid.ValueKind != JsonValueKind.Null
                        ? tid.GetUInt64() : null,
                    UserId = data.TryGetProperty("user_id", out JsonElement uid) && uid.ValueKind != JsonValueKind.Null
                        ? uid.GetUInt64() : null,
                    Reason = data.TryGetProperty("reason", out JsonElement r) && r.ValueKind != JsonValueKind.Null
                        ? r.GetString() : null
                };
                GatewayAuditLogEvent evt = new() { GuildId = guildId, Entry = entry };
                GuildAuditLogEntryCreate?.Invoke(this, evt);
            }
            catch (Exception ex) { Error?.Invoke(this, ex); }
        }

        // Message events
        else if (string.Equals(eventName, "MESSAGE_UPDATE", StringComparison.Ordinal))
        {
            TryEmitMessageUpdateEvent(data, MessageUpdate);
        }
        else if (string.Equals(eventName, "MESSAGE_DELETE", StringComparison.Ordinal))
        {
            TryEmitMessageDeleteEvent(data, MessageDelete);
        }
        else if (string.Equals(eventName, "MESSAGE_DELETE_BULK", StringComparison.Ordinal))
        {
            TryEmitMessageDeleteBulkEvent(data, MessageDeleteBulk);
        }

        // Reaction events
        else if (string.Equals(eventName, "MESSAGE_REACTION_ADD", StringComparison.Ordinal))
        {
            TryEmitReactionEvent(data, MessageReactionAdd);
        }
        else if (string.Equals(eventName, "MESSAGE_REACTION_REMOVE", StringComparison.Ordinal))
        {
            TryEmitReactionEvent(data, MessageReactionRemove);
        }
        else if (string.Equals(eventName, "MESSAGE_REACTION_REMOVE_ALL", StringComparison.Ordinal))
        {
            TryEmitMessageDeleteEvent(data, MessageReactionRemoveAll);
        }
        else if (string.Equals(eventName, "MESSAGE_REACTION_REMOVE_EMOJI", StringComparison.Ordinal))
        {
            TryEmitReactionRemoveEmojiEvent(data, MessageReactionRemoveEmoji);
        }

        // Interactions (slash commands, components, modals)
        else if (string.Equals(eventName, "INTERACTION_CREATE", StringComparison.Ordinal))
        {
            try
            {
                int type = data.GetProperty("type").GetInt32(); // 2=command, 3=component, 5=modal submit
                string id = data.GetProperty("id").GetString()!;
                string interaction_token = data.GetProperty("token").GetString()!;
                string appId = data.GetProperty("application_id").GetString()!;
                string? guildId = data.TryGetProperty("guild_id", out JsonElement gid) && gid.ValueKind != JsonValueKind.Null ? gid.GetString() : null;
                string? channelId = data.TryGetProperty("channel_id", out JsonElement chIdEl) && chIdEl.ValueKind != JsonValueKind.Null ? chIdEl.GetString() : null;

                Author? author = null;
                Entities.DiscordMember? member = null;

                if (data.TryGetProperty("member", out JsonElement memberObj))
                {
                    // Parse user from member
                    if (memberObj.TryGetProperty("user", out JsonElement mu))
                    {
                        author = new Author { Id = mu.GetProperty("id").GetUInt64(), Username = mu.TryGetProperty("username", out JsonElement un) ? (un.GetString() ?? string.Empty) : string.Empty };

                        // Parse full member object
                        DiscordUser user = new()
                        {
                            Id = mu.GetProperty("id").GetUInt64(),
                            Username = mu.TryGetProperty("username", out JsonElement uname) ? (uname.GetString() ?? string.Empty) : string.Empty,
                            Discriminator = mu.TryGetProperty("discriminator", out JsonElement disc) && disc.ValueKind == JsonValueKind.String ? ushort.Parse(disc.GetString()!) : (ushort)0
                        };

                        ulong[] roles = memberObj.TryGetProperty("roles", out JsonElement rolesEl) && rolesEl.ValueKind == JsonValueKind.Array
                            ? rolesEl.EnumerateArray().Select(r => r.GetUInt64()).ToArray()
                            : [];

                        string? nick = memberObj.TryGetProperty("nick", out JsonElement nickEl) && nickEl.ValueKind != JsonValueKind.Null ? nickEl.GetString() : null;

                        ulong? permissions = memberObj.TryGetProperty("permissions", out JsonElement permsEl) && permsEl.ValueKind == JsonValueKind.String
                            ? ulong.Parse(permsEl.GetString()!) : null;

                        // Create placeholder guild (will be replaced with actual guild in cache)
                        ulong parsedGuildId = guildId != null ? ulong.Parse(guildId) : 0;
                        DiscordGuild guild = new() { Id = parsedGuildId, Name = string.Empty };

                        member = new DiscordMember
                        {
                            User = user,
                            Guild = guild,
                            Nick = nick,
                            Roles = roles,
                            Permissions = permissions
                        };
                    }
                }
                else if (data.TryGetProperty("user", out JsonElement uo))
                {
                    author = new Author { Id = uo.GetProperty("id").GetUInt64(), Username = uo.TryGetProperty("username", out JsonElement un) ? (un.GetString() ?? string.Empty) : string.Empty };
                }

                // Switch by interaction type
                if (type == 2)
                {
                    // command data
                    JsonElement d = data.GetProperty("data");
                    string name = d.GetProperty("name").GetString()!;
                    string? sub = null;
                    List<InteractionOption> options = [];
                    if (d.TryGetProperty("options", out JsonElement opts) && opts.ValueKind == JsonValueKind.Array)
                    {
                        if (opts.GetArrayLength() > 0)
                        {
                            JsonElement first = opts[0];
                            if (first.TryGetProperty("type", out JsonElement tProp) && tProp.ValueKind == JsonValueKind.Number && tProp.GetInt32() == 1)
                            {
                                // SUB_COMMAND case
                                sub = first.GetProperty("name").GetString();
                                if (first.TryGetProperty("options", out JsonElement subOpts) && subOpts.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (JsonElement o in subOpts.EnumerateArray())
                                    {
                                        string oname = o.GetProperty("name").GetString()!;
                                        string? s = null; long? i = null; bool? b = null;
                                        if (o.TryGetProperty("value", out JsonElement val))
                                        {
                                            switch (val.ValueKind)
                                            {
                                                case JsonValueKind.String: s = val.GetString(); break;
                                                case JsonValueKind.Number: if (val.TryGetInt64(out long li)) i = li; break;
                                                case JsonValueKind.True: b = true; break;
                                                case JsonValueKind.False: b = false; break;
                                            }
                                        }
                                        options.Add(new InteractionOption { Name = oname, String = s, Integer = i, Boolean = b });
                                    }
                                }
                            }
                            else
                            {
                                // Flat options (no subcommand)
                                foreach (JsonElement o in opts.EnumerateArray())
                                {
                                    string oname = o.GetProperty("name").GetString()!;
                                    string? s = null; long? i = null; bool? b = null;
                                    if (o.TryGetProperty("value", out JsonElement val))
                                    {
                                        switch (val.ValueKind)
                                        {
                                            case JsonValueKind.String: s = val.GetString(); break;
                                            case JsonValueKind.Number: if (val.TryGetInt64(out long li)) i = li; break;
                                            case JsonValueKind.True: b = true; break;
                                            case JsonValueKind.False: b = false; break;
                                        }
                                    }
                                    options.Add(new InteractionOption { Name = oname, String = s, Integer = i, Boolean = b });
                                }
                            }
                        }
                    }

                    ApplicationCommandData cmd = new() { Name = name, Subcommand = sub, Options = options };
                    InteractionCreateEvent evt = new()
                    {
                        Id = id,
                        Token = interaction_token,
                        ApplicationId = appId,
                        Type = InteractionType.ApplicationCommand,
                        GuildId = guildId,
                        ChannelId = channelId,
                        Author = author,
                        Member = member,
                        Guild = null, // Will be populated by DiscordBot from cache
                        Data = cmd
                    };
                    InteractionCreate?.Invoke(this, evt);
                }
                else if (type == 3)
                {
                    // message component
                    JsonElement d = data.GetProperty("data");
                    string customId = d.GetProperty("custom_id").GetString()!;
                    int componentType = d.TryGetProperty("component_type", out JsonElement ctEl) && ctEl.ValueKind == JsonValueKind.Number ? ctEl.GetInt32() : 0;
                    string[]? values = null;
                    if (d.TryGetProperty("values", out JsonElement vEl) && vEl.ValueKind == JsonValueKind.Array)
                    {
                        values = vEl.EnumerateArray().Select(static x => x.GetString()!).ToArray();
                    }
                    string? messageId = null;
                    if (data.TryGetProperty("message", out JsonElement msgEl) && msgEl.TryGetProperty("id", out JsonElement mid))
                        messageId = mid.GetString();

                    MessageComponentData comp = new() { CustomId = customId, ComponentType = componentType, Values = values, MessageId = messageId };
                    InteractionCreateEvent evt = new()
                    {
                        Id = id,
                        Token = interaction_token,
                        ApplicationId = appId,
                        Type = InteractionType.MessageComponent,
                        GuildId = guildId,
                        ChannelId = channelId,
                        Author = author,
                        Member = member,
                        Guild = null, // Will be populated by DiscordBot from cache
                        Component = comp
                    };
                    InteractionCreate?.Invoke(this, evt);
                }
                else if (type == 5)
                {
                    // modal submit
                    JsonElement d = data.GetProperty("data");
                    string customId = d.GetProperty("custom_id").GetString()!;
                    List<TextInputValue> inputs = [];
                    if (d.TryGetProperty("components", out JsonElement rows) && rows.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement row in rows.EnumerateArray())
                        {
                            if (row.TryGetProperty("components", out JsonElement comps) && comps.ValueKind == JsonValueKind.Array)
                            {
                                foreach (JsonElement c in comps.EnumerateArray())
                                {
                                    // text input
                                    string? customIdField = c.TryGetProperty("custom_id", out JsonElement cidEl) ? cidEl.GetString() : null;
                                    string? val = c.TryGetProperty("value", out JsonElement valEl) ? (valEl.GetString() ?? string.Empty) : null;
                                    if (!string.IsNullOrEmpty(customIdField))
                                        inputs.Add(new TextInputValue { CustomId = customIdField!, Value = val });
                                }
                            }
                        }
                    }

                    ModalSubmitData modal = new() { CustomId = customId, Inputs = inputs };
                    InteractionCreateEvent evt = new()
                    {
                        Id = id,
                        Token = interaction_token,
                        ApplicationId = appId,
                        Type = InteractionType.ModalSubmit,
                        GuildId = guildId,
                        ChannelId = channelId,
                        Author = author,
                        Member = member,
                        Guild = null, // Will be populated by DiscordBot from cache
                        Modal = modal
                    };
                    InteractionCreate?.Invoke(this, evt);
                }
            }
            catch (Exception ex) { Error?.Invoke(this, ex); }
        }
    }

    public void Dispose()
    {
        try { _heartbeatTimer?.Dispose(); } catch { /* Timer disposal can throw, safe to ignore during cleanup */ }
        try { _ws.Dispose(); } catch { /* WebSocket disposal can throw, safe to ignore during cleanup */ }
        _internalCts.Dispose();
    }
}
