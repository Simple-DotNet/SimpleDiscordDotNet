using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Models;
using SimpleDiscordNet.Events;

namespace SimpleDiscordNet.Gateway;

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "GatewayClient uses JsonSerializerOptions configured with source-generated DiscordJsonContext for all gateway payload types.")]
[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "GatewayClient uses JsonSerializerOptions configured with source-generated DiscordJsonContext for all gateway payload types.")]
internal sealed partial class GatewayClient(
    string token,
    DiscordIntents intents,
    JsonSerializerOptions json,
    int? shardId = null,
    int? totalShards = null,
    Action<string>? debugLog = null,
    Uri? gatewayUri = null,
    Func<int, CancellationToken, Task>? identifyGate = null)
    : IDisposable
{
    private volatile ClientWebSocket _ws = new();
    private readonly CancellationTokenSource _internalCts = new();
    private Task? _loopTask;
    private long _seq;
    private volatile string? _sessionId;
    private volatile string? _resumeGatewayUrl;
    private int _heartbeatIntervalMs;
    private volatile bool _awaitingHeartbeatAck;
    private int _missedHeartbeatAcks;
    private int _reconnectAttempt;
    private volatile bool _sessionExpired;
    private volatile bool _autoReconnect = true;
    private int _isReady;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private CancellationTokenSource? _ctsHeartbeat;
    private readonly object _heartbeatLock = new();

    internal int? ShardId { get; } = shardId;
    internal int? TotalShards { get; } = totalShards;

    internal bool IsReady => Volatile.Read(ref _isReady) == 1;

    private void LogGatewaySend(int opcode, ReadOnlySpan<byte> payload)
    {
        if (debugLog is null) return;

        // Identify and Resume carry the bot token, so their contents are never logged
        debugLog(opcode is 2 or 6
            ? $"[GW SEND op={opcode}] (payload hidden: contains the bot token)"
            : $"[GW SEND op={opcode}] {Encoding.UTF8.GetString(payload)}");
    }

    public event EventHandler? Connected;
    public event EventHandler? SessionResumed;
    public event EventHandler<Exception?>? Disconnected;
    public event EventHandler? SessionReset;
    public event EventHandler<Exception>? Error;
    public event EventHandler<MessageCreateEventRaw>? MessageCreate;
    public event EventHandler<InteractionCreateEvent>? InteractionCreate;

    // Domain events (internal) to be surfaced by DiscordBot
    public event EventHandler<GuildCreateEvent>? GuildCreate;
    public event EventHandler<DiscordGuild>? GuildUpdate;
    public event EventHandler<ulong>? GuildDelete; // guild id
    public event EventHandler<GuildEmojisUpdateEvent>? GuildEmojisUpdate;
    public event EventHandler<VoiceStateUpdateEvent>? VoiceStateUpdate;
    public event EventHandler<PresenceUpdateEvent>? PresenceUpdate;
    public event EventHandler<TypingStartEvent>? TypingStart;
    public event EventHandler<WebhooksUpdateEvent>? WebhooksUpdate;
    public event EventHandler<InviteCreateEvent>? InviteCreate;
    public event EventHandler<InviteDeleteEvent>? InviteDelete;
    public event EventHandler<GuildIntegrationsUpdateEvent>? GuildIntegrationsUpdate;

    public event EventHandler<AutoModerationRuleCreatedEvent>? AutoModerationRuleCreated;
    public event EventHandler<AutoModerationRuleUpdatedEvent>? AutoModerationRuleUpdated;
    public event EventHandler<AutoModerationRuleDeletedEvent>? AutoModerationRuleDeleted;
    public event EventHandler<AutoModerationActionExecutionEvent>? AutoModerationActionExecution;
    public event EventHandler<StageInstanceCreatedEvent>? StageInstanceCreated;
    public event EventHandler<StageInstanceUpdatedEvent>? StageInstanceUpdated;
    public event EventHandler<StageInstanceDeletedEvent>? StageInstanceDeleted;
    public event EventHandler<GuildScheduledEventCreatedEvent>? GuildScheduledEventCreated;
    public event EventHandler<GuildScheduledEventUpdatedEvent>? GuildScheduledEventUpdated;
    public event EventHandler<GuildScheduledEventDeletedEvent>? GuildScheduledEventDeleted;
    public event EventHandler<GuildScheduledEventUserAddedEvent>? GuildScheduledEventUserAdded;
    public event EventHandler<GuildScheduledEventUserRemovedEvent>? GuildScheduledEventUserRemoved;
    public event EventHandler<IntegrationCreatedEvent>? IntegrationCreated;
    public event EventHandler<IntegrationUpdatedEvent>? IntegrationUpdated;
    public event EventHandler<IntegrationDeletedEvent>? IntegrationDeleted;
    public event EventHandler<VoiceServerUpdateEvent>? VoiceServerUpdate;
    public event EventHandler<GuildJoinRequestCreatedEvent>? GuildJoinRequestCreated;
    public event EventHandler<GuildJoinRequestUpdatedEvent>? GuildJoinRequestUpdated;
    public event EventHandler<GuildJoinRequestDeletedEvent>? GuildJoinRequestDeleted;
    public event EventHandler<PollVoteAddedEvent>? PollVoteAdded;
    public event EventHandler<PollVoteRemovedEvent>? PollVoteRemoved;

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
        if (_loopTask != null) throw new InvalidOperationException("This gateway client has already been started.");
        await ConnectSocketAsync(cancellationToken).ConfigureAwait(false);
        _loopTask = Task.Run(() => ReceiveLoop(_internalCts.Token), cancellationToken);
    }

    public async Task DisconnectAsync()
    {
        try
        {
            _autoReconnect = false;
            await _internalCts.CancelAsync();
            StopHeartbeat();

            // A clean shutdown closes with 1000, which tells Discord the session is finished
            ClientWebSocket ws = _ws;
            if (ws.State == WebSocketState.Open)
            {
                using CancellationTokenSource timeout = new(CloseTimeout);
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "shutdown", timeout.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, ex);
        }
        finally
        {
            Disconnected?.Invoke(this, null);
        }
    }

    internal void HandleDispatch(string? eventName, JsonElement data)
    {
        try
        {
            switch (eventName)
            {
                case "READY":
                    HandleReady(data);
                    break;
                case "RESUMED":
                    _sessionExpired = false;
                    Interlocked.Exchange(ref _reconnectAttempt, 0);
                    SessionResumed?.Invoke(this, EventArgs.Empty);
                    break;

                case "MESSAGE_CREATE":
                    HandleMessageCreate(data);
                    break;

                // Guild events
                case "GUILD_CREATE":
                    HandleGuildCreate(data);
                    break;
                case "GUILD_UPDATE":
                    GuildUpdate?.Invoke(this, ParseGuild(data));
                    break;
                case "GUILD_DELETE":
                    // "unavailable" means an outage, not that the bot left, so keep the cached guild
                    if (GetBoolOrNull(data, "unavailable") != true)
                    {
                        GuildDelete?.Invoke(this, data.GetProperty("id").GetDiscordId());
                    }
                    break;
                case "GUILD_EMOJIS_UPDATE":
                    GuildEmojisUpdate?.Invoke(this, new GuildEmojisUpdateEvent
                    {
                        GuildId = data.GetProperty("guild_id").GetDiscordId(),
                        Emojis = ParseEmojis(data) ?? []
                    });
                    break;

                // Channel events
                case "CHANNEL_CREATE":
                    TryEmitChannelEvent(data, ChannelCreate);
                    break;
                case "CHANNEL_UPDATE":
                    TryEmitChannelEvent(data, ChannelUpdate);
                    break;
                case "CHANNEL_DELETE":
                    TryEmitChannelEvent(data, ChannelDelete);
                    break;

                // Role events
                case "GUILD_ROLE_CREATE":
                    TryEmitRoleEvent(data, GuildRoleCreate);
                    break;
                case "GUILD_ROLE_UPDATE":
                    TryEmitRoleEvent(data, GuildRoleUpdate);
                    break;
                case "GUILD_ROLE_DELETE":
                    TryEmitRoleDeleteEvent(data, GuildRoleDelete);
                    break;

                // Thread events
                case "THREAD_CREATE":
                    TryEmitChannelEvent(data, ThreadCreate);
                    break;
                case "THREAD_UPDATE":
                    TryEmitChannelEvent(data, ThreadUpdate);
                    break;
                case "THREAD_DELETE":
                    TryEmitChannelEvent(data, ThreadDelete);
                    break;
                case "THREAD_MEMBER_UPDATE":
                    TryEmitThreadMemberUpdate(data);
                    break;
                case "THREAD_MEMBERS_UPDATE":
                    TryEmitThreadMembersUpdate(data);
                    break;
                case "THREAD_LIST_SYNC":
                    TryEmitThreadListSync(data);
                    break;
                case "CHANNEL_PINS_UPDATE":
                    TryEmitChannelPinsUpdate(data);
                    break;
                case "RATE_LIMITED":
                    TryEmitGatewayRateLimited(data);
                    break;

                // Member events
                case "GUILD_MEMBER_ADD":
                    TryEmitMemberEvent(data, GuildMemberAdd);
                    break;
                case "GUILD_MEMBER_UPDATE":
                    TryEmitMemberEvent(data, GuildMemberUpdate);
                    break;
                case "GUILD_MEMBER_REMOVE":
                    TryEmitMemberRemoveEvent(data, GuildMemberRemove);
                    break;
                case "GUILD_MEMBERS_CHUNK":
                    HandleGuildMembersChunk(data);
                    break;

                // Ban events
                case "GUILD_BAN_ADD":
                    TryEmitBanEvent(data, GuildBanAdd);
                    break;
                case "GUILD_BAN_REMOVE":
                    TryEmitBanEvent(data, GuildBanRemove);
                    break;

                // Bot user update
                case "USER_UPDATE":
                    UserUpdate?.Invoke(this, ParseUser(data));
                    break;

                // Audit log events
                case "GUILD_AUDIT_LOG_ENTRY_CREATE":
                    GuildAuditLogEntryCreate?.Invoke(this, new GatewayAuditLogEvent
                    {
                        GuildId = data.GetProperty("guild_id").GetDiscordId(),
                        Entry = data.Deserialize<DiscordAuditLogEntry>(json) ?? throw new JsonException("Audit log entry was null.")
                    });
                    break;

                // Message events
                case "MESSAGE_UPDATE":
                    TryEmitMessageUpdateEvent(data, MessageUpdate);
                    break;
                case "MESSAGE_DELETE":
                    TryEmitMessageDeleteEvent(data, MessageDelete);
                    break;
                case "MESSAGE_DELETE_BULK":
                    TryEmitMessageDeleteBulkEvent(data, MessageDeleteBulk);
                    break;

                // Reaction events
                case "MESSAGE_REACTION_ADD":
                    TryEmitReactionEvent(data, MessageReactionAdd);
                    break;
                case "MESSAGE_REACTION_REMOVE":
                    TryEmitReactionEvent(data, MessageReactionRemove);
                    break;
                case "MESSAGE_REACTION_REMOVE_ALL":
                    TryEmitMessageDeleteEvent(data, MessageReactionRemoveAll);
                    break;
                case "MESSAGE_REACTION_REMOVE_EMOJI":
                    TryEmitReactionRemoveEmojiEvent(data, MessageReactionRemoveEmoji);
                    break;

                // Interactions (slash commands, components, modals)
                case "INTERACTION_CREATE":
                    HandleInteractionCreate(data);
                    break;

                // Voice events
                case "VOICE_STATE_UPDATE":
                    TryEmitVoiceStateUpdateEvent(data, VoiceStateUpdate);
                    break;
                case "VOICE_SERVER_UPDATE":
                    TryEmitVoiceServerUpdateEvent(data, VoiceServerUpdate);
                    break;

                // Presence, typing, webhook and invite events
                case "PRESENCE_UPDATE":
                    TryEmitPresenceUpdateEvent(data, PresenceUpdate);
                    break;
                case "TYPING_START":
                    TryEmitTypingStartEvent(data, TypingStart);
                    break;
                case "WEBHOOKS_UPDATE":
                    TryEmitWebhooksUpdateEvent(data, WebhooksUpdate);
                    break;
                case "INVITE_CREATE":
                    TryEmitInviteCreateEvent(data, InviteCreate);
                    break;
                case "INVITE_DELETE":
                    TryEmitInviteDeleteEvent(data, InviteDelete);
                    break;
                case "GUILD_INTEGRATIONS_UPDATE":
                    TryEmitGuildIntegrationsUpdateEvent(data, GuildIntegrationsUpdate);
                    break;

                // Auto Moderation events
                case "AUTO_MODERATION_RULE_CREATE":
                    TryEmitAutoModerationRuleCreatedEvent(data, AutoModerationRuleCreated);
                    break;
                case "AUTO_MODERATION_RULE_UPDATE":
                    TryEmitAutoModerationRuleUpdatedEvent(data, AutoModerationRuleUpdated);
                    break;
                case "AUTO_MODERATION_RULE_DELETE":
                    TryEmitAutoModerationRuleDeletedEvent(data, AutoModerationRuleDeleted);
                    break;
                case "AUTO_MODERATION_ACTION_EXECUTION":
                    TryEmitAutoModerationActionExecutionEvent(data, AutoModerationActionExecution);
                    break;

                // Stage Instance events
                case "STAGE_INSTANCE_CREATE":
                    TryEmitStageInstanceCreatedEvent(data, StageInstanceCreated);
                    break;
                case "STAGE_INSTANCE_UPDATE":
                    TryEmitStageInstanceUpdatedEvent(data, StageInstanceUpdated);
                    break;
                case "STAGE_INSTANCE_DELETE":
                    TryEmitStageInstanceDeletedEvent(data, StageInstanceDeleted);
                    break;

                // Guild Scheduled Event events
                case "GUILD_SCHEDULED_EVENT_CREATE":
                    TryEmitGuildScheduledEventCreatedEvent(data, GuildScheduledEventCreated);
                    break;
                case "GUILD_SCHEDULED_EVENT_UPDATE":
                    TryEmitGuildScheduledEventUpdatedEvent(data, GuildScheduledEventUpdated);
                    break;
                case "GUILD_SCHEDULED_EVENT_DELETE":
                    TryEmitGuildScheduledEventDeletedEvent(data, GuildScheduledEventDeleted);
                    break;
                case "GUILD_SCHEDULED_EVENT_USER_ADD":
                    TryEmitGuildScheduledEventUserAddedEvent(data, GuildScheduledEventUserAdded);
                    break;
                case "GUILD_SCHEDULED_EVENT_USER_REMOVE":
                    TryEmitGuildScheduledEventUserRemovedEvent(data, GuildScheduledEventUserRemoved);
                    break;

                // Integration events
                case "INTEGRATION_CREATE":
                    TryEmitIntegrationCreatedEvent(data, IntegrationCreated);
                    break;
                case "INTEGRATION_UPDATE":
                    TryEmitIntegrationUpdatedEvent(data, IntegrationUpdated);
                    break;
                case "INTEGRATION_DELETE":
                    TryEmitIntegrationDeletedEvent(data, IntegrationDeleted);
                    break;

                // Guild Join Request events
                case "GUILD_JOIN_REQUEST_CREATE":
                    TryEmitGuildJoinRequestCreatedEvent(data, GuildJoinRequestCreated);
                    break;
                case "GUILD_JOIN_REQUEST_UPDATE":
                    TryEmitGuildJoinRequestUpdatedEvent(data, GuildJoinRequestUpdated);
                    break;
                case "GUILD_JOIN_REQUEST_DELETE":
                    TryEmitGuildJoinRequestDeletedEvent(data, GuildJoinRequestDeleted);
                    break;

                // Poll Vote events
                case "MESSAGE_POLL_VOTE_ADD":
                    TryEmitPollVoteAddedEvent(data, PollVoteAdded);
                    break;
                case "MESSAGE_POLL_VOTE_REMOVE":
                    TryEmitPollVoteRemovedEvent(data, PollVoteRemoved);
                    break;
            }
        }
        catch (Exception ex)
        {
            // One bad payload must never take the receive loop down
            Error?.Invoke(this, ex);
        }
    }

    private void HandleReady(JsonElement data)
    {
        _sessionId = GetStringOrNull(data, "session_id");
        _resumeGatewayUrl = GetStringOrNull(data, "resume_gateway_url");

        // Extract bot user information from READY event
        if (data.TryGetProperty("user", out JsonElement userObj))
        {
            UserUpdate?.Invoke(this, ParseUser(userObj));
        }

        _sessionExpired = false;
        Interlocked.Exchange(ref _reconnectAttempt, 0);

        if (Interlocked.CompareExchange(ref _isReady, 1, 0) == 0)
        {
            Connected?.Invoke(this, EventArgs.Empty);
        }
    }

    private void HandleMessageCreate(JsonElement data)
    {
        Author author;
        DiscordUser? authorUser = null;
        if (data.TryGetProperty("author", out JsonElement authorObj))
        {
            authorUser = ParseUser(authorObj);
            author = new Author { Id = authorUser.Id, Username = authorUser.Username, Bot = authorUser.Bot };
        }
        else
        {
            author = new Author { Id = 0, Username = "Unknown" };
        }

        MessageCreate?.Invoke(this, new MessageCreateEventRaw
        {
            Id = data.GetProperty("id").GetString()!,
            ChannelId = data.GetProperty("channel_id").GetString()!,
            GuildId = GetStringOrNull(data, "guild_id"),
            Content = GetStringOrNull(data, "content") ?? string.Empty,
            Author = author,
            AuthorUser = authorUser
        });
    }

    private void HandleGuildCreate(JsonElement data)
    {
        DiscordGuild guild = ParseGuild(data);

        GuildCreate?.Invoke(this, new GuildCreateEvent
        {
            Guild = guild,
            Channels = ParseChannels(data, "channels", guild.Id),
            Members = ParseMembers(data, guild),
            Threads = ParseChannels(data, "threads", guild.Id)
        });
    }

    private void HandleGuildMembersChunk(JsonElement data)
    {
        ulong guildId = data.GetProperty("guild_id").GetDiscordId();

        // Placeholder guild, replaced with the cached guild when the members are stored
        DiscordGuild guild = new() { Id = guildId, Name = string.Empty };

        GuildMembersChunk?.Invoke(this, new GuildMembersChunkEvent
        {
            GuildId = guildId,
            Members = ParseMembers(data, guild),
            ChunkIndex = GetIntOrNull(data, "chunk_index") ?? 0,
            ChunkCount = GetIntOrNull(data, "chunk_count") ?? 1
        });
    }

    private void HandleInteractionCreate(JsonElement data)
    {
        int type = data.GetProperty("type").GetInt32(); // 2=command, 3=component, 4=autocomplete, 5=modal submit
        string? guildId = GetStringOrNull(data, "guild_id");
        ulong parsedGuildId = guildId != null ? ulong.Parse(guildId, CultureInfo.InvariantCulture) : 0;

        Author? author = null;
        DiscordMember? member = null;

        if (data.TryGetProperty("member", out JsonElement memberObj))
        {
            if (memberObj.TryGetProperty("user", out JsonElement memberUser))
            {
                DiscordUser user = ParseUser(memberUser);
                author = new Author { Id = user.Id, Username = user.Username, Bot = user.Bot };

                // Placeholder guild, replaced with the cached guild by DiscordBot
                member = ParseMember(memberObj, user, new DiscordGuild { Id = parsedGuildId, Name = string.Empty });
            }
        }
        else if (data.TryGetProperty("user", out JsonElement userObj))
        {
            DiscordUser user = ParseUser(userObj);
            author = new Author { Id = user.Id, Username = user.Username, Bot = user.Bot };
        }

        InteractionCreateEvent evt = new()
        {
            Id = data.GetProperty("id").GetString()!,
            Token = data.GetProperty("token").GetString()!,
            ApplicationId = data.GetProperty("application_id").GetString()!,
            GuildId = guildId,
            ChannelId = GetStringOrNull(data, "channel_id"),
            Author = author,
            Member = member,
            Guild = null, // Will be populated by DiscordBot from cache
            ShardId = ShardId,
            AppPermissions = GetIdOrNull(data, "app_permissions")
        };

        JsonElement interactionData = data.TryGetProperty("data", out JsonElement d) ? d : default;
        switch (type)
        {
            case 2:
                InteractionCreate?.Invoke(this, evt with
                {
                    Type = InteractionType.ApplicationCommand,
                    Data = ParseCommandData(interactionData, parsedGuildId)
                });
                break;

            case 3:
                string[]? values = null;
                if (interactionData.TryGetProperty("values", out JsonElement valuesEl) && valuesEl.ValueKind == JsonValueKind.Array)
                {
                    values = valuesEl.EnumerateArray().Select(static x => x.GetString()!).ToArray();
                }
                string? messageId = data.TryGetProperty("message", out JsonElement messageEl) ? GetStringOrNull(messageEl, "id") : null;

                InteractionCreate?.Invoke(this, evt with
                {
                    Type = InteractionType.MessageComponent,
                    Component = new MessageComponentData
                    {
                        CustomId = interactionData.GetProperty("custom_id").GetString()!,
                        ComponentType = GetIntOrNull(interactionData, "component_type") ?? 0,
                        Values = values,
                        MessageId = messageId,
                        Resolved = ParseResolved(interactionData, parsedGuildId)
                    }
                });
                break;

            case 4:
                InteractionCreate?.Invoke(this, evt with
                {
                    Type = InteractionType.ApplicationCommandAutocomplete,
                    Data = ParseCommandData(interactionData, parsedGuildId)
                });
                break;

            case 5:
                List<TextInputValue> inputs = [];
                CollectModalInputs(interactionData, inputs);

                InteractionCreate?.Invoke(this, evt with
                {
                    Type = InteractionType.ModalSubmit,
                    Modal = new ModalSubmitData
                    {
                        CustomId = interactionData.GetProperty("custom_id").GetString()!,
                        Inputs = inputs,
                        Resolved = ParseResolved(interactionData, parsedGuildId)
                    }
                });
                break;
        }
    }

    public void Dispose()
    {
        _autoReconnect = false;
        try { _internalCts.Cancel(); } catch { /* Cancellation may throw, safe to ignore during cleanup */ }
        StopHeartbeat();
        ClientWebSocket ws = _ws;
        try { ws.Abort(); } catch { /* Abort can throw on a disposed socket, safe to ignore during cleanup */ }
        if (_loopTask != null)
        {
            try { _loopTask.Wait(TimeSpan.FromSeconds(5)); } catch { /* Task may fault or timeout, safe to ignore */ }
            try { _loopTask.Dispose(); } catch { /* Task disposal can throw, safe to ignore */ }
        }
        try { _ws.Dispose(); } catch { /* WebSocket disposal can throw, safe to ignore during cleanup */ }
        try { _internalCts.Dispose(); } catch { /* CTS disposal can throw, safe to ignore during cleanup */ }
        try { _writeLock.Dispose(); } catch { /* SemaphoreSlim disposal can throw, safe to ignore during cleanup */ }
    }
}
