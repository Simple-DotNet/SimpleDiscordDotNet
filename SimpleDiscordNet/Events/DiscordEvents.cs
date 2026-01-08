using SimpleDiscordNet.Logging;
using SimpleDiscordNet.Models;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Global static event hub for Discord bot events. Consumers can subscribe
/// from any project without holding a DiscordBot instance.
/// </summary>
public static class DiscordEvents
{
    // ---- Connection and logging ----
    /// <summary>Fired when the bot successfully connects to Discord's gateway.</summary>
    public static event EventHandler? Connected;
    /// <summary>Fired when the bot disconnects from Discord's gateway.</summary>
    public static event EventHandler<Exception?>? Disconnected;
    /// <summary>Fired when an error occurs in the bot.</summary>
    public static event EventHandler<Exception>? Error;

    /// <summary>Fired when the bot logs a message.</summary>
    public static event EventHandler<LogMessage>? Log;

    internal static void RaiseConnected(object? sender)
        => Connected?.Invoke(sender, EventArgs.Empty);
    internal static void RaiseDisconnected(object? sender, Exception? ex)
        => Disconnected?.Invoke(sender, ex);
    internal static void RaiseError(object? sender, Exception ex)
        => Error?.Invoke(sender, ex);
    internal static void RaiseLog(object? sender, LogMessage msg)
        => Log?.Invoke(sender, msg);

    // ---- Domain events ----
    /// <summary>Fired when the bot joins a guild or receives GUILD_CREATE from Discord.</summary>
    public static event EventHandler<GuildEvent>? GuildAdded;
    /// <summary>Fired when a guild's information is updated.</summary>
    public static event EventHandler<GuildEvent>? GuildUpdated;
    /// <summary>Fired when the bot leaves or is removed from a guild. Event argument is the guild ID.</summary>
    public static event EventHandler<string>? GuildRemoved;
    /// <summary>
    /// Fired when a guild is fully loaded with all members, channels, roles, and emojis.
    /// Only fires when AutoLoadFullGuildData is enabled (default).
    /// </summary>
    public static event EventHandler<GuildEvent>? GuildReady;

    /// <summary>Fired when a channel is created in a guild.</summary>
    public static event EventHandler<ChannelEvent>? ChannelCreated;
    /// <summary>Fired when a channel is updated in a guild.</summary>
    public static event EventHandler<ChannelEvent>? ChannelUpdated;
    /// <summary>Fired when a channel is deleted from a guild.</summary>
    public static event EventHandler<ChannelEvent>? ChannelDeleted;

    /// <summary>Fired when a role is created in a guild.</summary>
    public static event EventHandler<RoleEvent>? RoleCreated;
    /// <summary>Fired when a role is updated in a guild.</summary>
    public static event EventHandler<RoleEvent>? RoleUpdated;
    /// <summary>Fired when a role is deleted from a guild.</summary>
    public static event EventHandler<RoleEvent>? RoleDeleted;

    /// <summary>Fired when a thread is created in a guild.</summary>
    public static event EventHandler<ThreadEvent>? ThreadCreated;
    /// <summary>Fired when a thread is updated in a guild.</summary>
    public static event EventHandler<ThreadEvent>? ThreadUpdated;
    /// <summary>Fired when a thread is deleted from a guild.</summary>
    public static event EventHandler<ThreadEvent>? ThreadDeleted;

    /// <summary>Fired when a message is created in any channel the bot can see.</summary>
    public static event EventHandler<MessageCreateEvent>? MessageCreated;
    /// <summary>Fired when a message is edited.</summary>
    public static event EventHandler<MessageUpdateEvent>? MessageUpdated;
    /// <summary>Fired when a message is deleted.</summary>
    public static event EventHandler<MessageEvent>? MessageDeleted;
    /// <summary>Fired when multiple messages are deleted at once (bulk delete).</summary>
    public static event EventHandler<MessageEvent>? MessagesBulkDeleted;

    /// <summary>Fired when a reaction is added to a message.</summary>
    public static event EventHandler<ReactionEvent>? ReactionAdded;
    /// <summary>Fired when a reaction is removed from a message.</summary>
    public static event EventHandler<ReactionEvent>? ReactionRemoved;
    /// <summary>Fired when all reactions for a specific emoji are removed from a message.</summary>
    public static event EventHandler<ReactionEvent>? ReactionsClearedForEmoji;
    /// <summary>Fired when all reactions are removed from a message.</summary>
    public static event EventHandler<MessageEvent>? ReactionsCleared;

    /// <summary>Fired when a member joins a guild. Requires GUILD_MEMBERS intent.</summary>
    public static event EventHandler<MemberEvent>? MemberJoined;
    /// <summary>Fired when a member's information is updated (nickname, roles, etc). Requires GUILD_MEMBERS intent.</summary>
    public static event EventHandler<MemberEvent>? MemberUpdated;
    /// <summary>Fired when a member leaves a guild (includes kicks and bans). Requires GUILD_MEMBERS intent.</summary>
    public static event EventHandler<MemberEvent>? MemberLeft;

    /// <summary>Fired when a chunk of guild members is received via REQUEST_GUILD_MEMBERS. Requires GUILD_MEMBERS intent.</summary>
    public static event EventHandler<GuildMembersChunkEvent>? GuildMembersChunk;

    /// <summary>Fired when a member is banned from a guild. Requires GUILD_MODERATION intent.</summary>
    public static event EventHandler<BanEvent>? BanAdded;
    /// <summary>Fired when a member is unbanned from a guild. Requires GUILD_MODERATION intent.</summary>
    public static event EventHandler<BanEvent>? BanRemoved;

    /// <summary>Fired when the bot's user information is updated.</summary>
    public static event EventHandler<BotUserEvent>? BotUserUpdated;

    // Audit logs
    /// <summary>
    /// Fired when a new audit log entry is created in a guild.
    /// Requires GUILD_MODERATION intent.
    /// </summary>
    public static event EventHandler<AuditLogEvent>? AuditLogEntryCreated;

    // Interactions
    /// <summary>Fired when an interaction (slash command, button, select menu, etc) is created.</summary>
    public static event EventHandler<InteractionCreateEvent>? InteractionCreated;

    // Guild emojis
    /// <summary>Fired when a guild's emojis are updated.</summary>
    public static event EventHandler<GuildEmojisUpdateEvent>? GuildEmojisUpdated;

    // Voice
    /// <summary>Fired when a user's voice state changes (joins/leaves/mutes/deafens). Requires GUILD_VOICE_STATES intent.</summary>
    public static event EventHandler<VoiceStateUpdateEvent>? VoiceStateUpdated;

    // Presence
    /// <summary>Fired when a user's presence updates (status, activity, etc). Requires GUILD_PRESENCES intent.</summary>
    public static event EventHandler<PresenceUpdateEvent>? PresenceUpdated;

    // Typing
    /// <summary>Fired when a user starts typing in a channel. Requires GUILD_MESSAGE_TYPING or DM_MESSAGE_TYPING intent.</summary>
    public static event EventHandler<TypingStartEvent>? TypingStarted;

    // Webhooks
    /// <summary>Fired when a channel's webhooks are updated.</summary>
    public static event EventHandler<WebhooksUpdateEvent>? WebhooksUpdated;

    // Invites
    /// <summary>Fired when an invite is created. Requires GUILD_INVITES intent.</summary>
    public static event EventHandler<InviteCreateEvent>? InviteCreated;
    /// <summary>Fired when an invite is deleted. Requires GUILD_INVITES intent.</summary>
    public static event EventHandler<InviteDeleteEvent>? InviteDeleted;
    // Integrations
    /// <summary>Fired when a guild's integrations are updated.</summary>
    public static event EventHandler<GuildIntegrationsUpdateEvent>? GuildIntegrationsUpdated;

    // Direct messages
    /// <summary>Fired when a direct message is received.</summary>
    public static event EventHandler<DirectMessageEvent>? DirectMessageReceived;

    // Auto Moderation
    /// <summary>Fired when an auto-moderation rule is created. Requires GUILD_MODERATION intent.</summary>
    public static event EventHandler<AutoModerationRuleCreatedEvent>? AutoModerationRuleCreated;
    /// <summary>Fired when an auto-moderation rule is updated. Requires GUILD_MODERATION intent.</summary>
    public static event EventHandler<AutoModerationRuleUpdatedEvent>? AutoModerationRuleUpdated;
    /// <summary>Fired when an auto-moderation rule is deleted. Requires GUILD_MODERATION intent.</summary>
    public static event EventHandler<AutoModerationRuleDeletedEvent>? AutoModerationRuleDeleted;
    /// <summary>Fired when an auto-moderation action is executed. Requires GUILD_MODERATION intent.</summary>
    public static event EventHandler<AutoModerationActionExecutionEvent>? AutoModerationActionExecution;

    // Stage Instance
    /// <summary>Fired when a stage instance is created.</summary>
    public static event EventHandler<StageInstanceCreatedEvent>? StageInstanceCreated;
    /// <summary>Fired when a stage instance is updated.</summary>
    public static event EventHandler<StageInstanceUpdatedEvent>? StageInstanceUpdated;
    /// <summary>Fired when a stage instance is deleted.</summary>
    public static event EventHandler<StageInstanceDeletedEvent>? StageInstanceDeleted;

    // Guild Scheduled Event
    /// <summary>Fired when a guild scheduled event is created.</summary>
    public static event EventHandler<GuildScheduledEventCreatedEvent>? GuildScheduledEventCreated;
    /// <summary>Fired when a guild scheduled event is updated.</summary>
    public static event EventHandler<GuildScheduledEventUpdatedEvent>? GuildScheduledEventUpdated;
    /// <summary>Fired when a guild scheduled event is deleted.</summary>
    public static event EventHandler<GuildScheduledEventDeletedEvent>? GuildScheduledEventDeleted;
    /// <summary>Fired when a user subscribes to a guild scheduled event.</summary>
    public static event EventHandler<GuildScheduledEventUserAddedEvent>? GuildScheduledEventUserAdded;
    /// <summary>Fired when a user unsubscribes from a guild scheduled event.</summary>
    public static event EventHandler<GuildScheduledEventUserRemovedEvent>? GuildScheduledEventUserRemoved;

    // Integration
    /// <summary>Fired when a guild integration is created.</summary>
    public static event EventHandler<IntegrationCreatedEvent>? IntegrationCreated;
    /// <summary>Fired when a guild integration is updated.</summary>
    public static event EventHandler<IntegrationUpdatedEvent>? IntegrationUpdated;
    /// <summary>Fired when a guild integration is deleted.</summary>
    public static event EventHandler<IntegrationDeletedEvent>? IntegrationDeleted;

    // Voice Server
    /// <summary>Fired when a voice server is updated for a guild.</summary>
    public static event EventHandler<VoiceServerUpdateEvent>? VoiceServerUpdated;

    // Guild Join Request
    /// <summary>Fired when a guild join request is created.</summary>
    public static event EventHandler<GuildJoinRequestCreatedEvent>? GuildJoinRequestCreated;
    /// <summary>Fired when a guild join request is updated.</summary>
    public static event EventHandler<GuildJoinRequestUpdatedEvent>? GuildJoinRequestUpdated;
    /// <summary>Fired when a guild join request is deleted.</summary>
    public static event EventHandler<GuildJoinRequestDeletedEvent>? GuildJoinRequestDeleted;

    // Poll Vote
    /// <summary>Fired when a vote is added to a poll.</summary>
    public static event EventHandler<PollVoteAddedEvent>? PollVoteAdded;
    /// <summary>Fired when a vote is removed from a poll.</summary>
    public static event EventHandler<PollVoteRemovedEvent>? PollVoteRemoved;

    internal static void RaiseGuildAdded(object? sender, GuildEvent e) => GuildAdded?.Invoke(sender, e);
    internal static void RaiseGuildUpdated(object? sender, GuildEvent e) => GuildUpdated?.Invoke(sender, e);
    internal static void RaiseGuildRemoved(object? sender, string id) => GuildRemoved?.Invoke(sender, id);
    internal static void RaiseGuildReady(object? sender, GuildEvent e) => GuildReady?.Invoke(sender, e);

    internal static void RaiseChannelCreated(object? sender, ChannelEvent e) => ChannelCreated?.Invoke(sender, e);
    internal static void RaiseChannelUpdated(object? sender, ChannelEvent e) => ChannelUpdated?.Invoke(sender, e);
    internal static void RaiseChannelDeleted(object? sender, ChannelEvent e) => ChannelDeleted?.Invoke(sender, e);

    internal static void RaiseRoleCreated(object? sender, RoleEvent e) => RoleCreated?.Invoke(sender, e);
    internal static void RaiseRoleUpdated(object? sender, RoleEvent e) => RoleUpdated?.Invoke(sender, e);
    internal static void RaiseRoleDeleted(object? sender, RoleEvent e) => RoleDeleted?.Invoke(sender, e);

    internal static void RaiseThreadCreated(object? sender, ThreadEvent e) => ThreadCreated?.Invoke(sender, e);
    internal static void RaiseThreadUpdated(object? sender, ThreadEvent e) => ThreadUpdated?.Invoke(sender, e);
    internal static void RaiseThreadDeleted(object? sender, ThreadEvent e) => ThreadDeleted?.Invoke(sender, e);

    internal static void RaiseMessageCreated(object? sender, MessageCreateEvent e) => MessageCreated?.Invoke(sender, e);
    internal static void RaiseMessageUpdated(object? sender, MessageUpdateEvent e) => MessageUpdated?.Invoke(sender, e);
    internal static void RaiseMessageDeleted(object? sender, MessageEvent e) => MessageDeleted?.Invoke(sender, e);
    internal static void RaiseMessagesBulkDeleted(object? sender, MessageEvent e) => MessagesBulkDeleted?.Invoke(sender, e);

    internal static void RaiseReactionAdded(object? sender, ReactionEvent e) => ReactionAdded?.Invoke(sender, e);
    internal static void RaiseReactionRemoved(object? sender, ReactionEvent e) => ReactionRemoved?.Invoke(sender, e);
    internal static void RaiseReactionsClearedForEmoji(object? sender, ReactionEvent e) => ReactionsClearedForEmoji?.Invoke(sender, e);
    internal static void RaiseReactionsCleared(object? sender, MessageEvent e) => ReactionsCleared?.Invoke(sender, e);

    internal static void RaiseMemberJoined(object? sender, MemberEvent e) => MemberJoined?.Invoke(sender, e);
    internal static void RaiseMemberUpdated(object? sender, MemberEvent e) => MemberUpdated?.Invoke(sender, e);
    internal static void RaiseMemberLeft(object? sender, MemberEvent e) => MemberLeft?.Invoke(sender, e);

    internal static void RaiseGuildMembersChunk(object? sender, GuildMembersChunkEvent e) => GuildMembersChunk?.Invoke(sender, e);

    internal static void RaiseBanAdded(object? sender, BanEvent e) => BanAdded?.Invoke(sender, e);
    internal static void RaiseBanRemoved(object? sender, BanEvent e) => BanRemoved?.Invoke(sender, e);

    internal static void RaiseBotUserUpdated(object? sender, BotUserEvent e) => BotUserUpdated?.Invoke(sender, e);

    internal static void RaiseAuditLogEntryCreated(object? sender, AuditLogEvent e) => AuditLogEntryCreated?.Invoke(sender, e);

    internal static void RaiseInteractionCreated(object? sender, InteractionCreateEvent e) => InteractionCreated?.Invoke(sender, e);
    internal static void RaiseGuildEmojisUpdated(object? sender, GuildEmojisUpdateEvent e) => GuildEmojisUpdated?.Invoke(sender, e);
    internal static void RaiseVoiceStateUpdated(object? sender, VoiceStateUpdateEvent e) => VoiceStateUpdated?.Invoke(sender, e);
    internal static void RaisePresenceUpdated(object? sender, PresenceUpdateEvent e) => PresenceUpdated?.Invoke(sender, e);
    internal static void RaiseTypingStarted(object? sender, TypingStartEvent e) => TypingStarted?.Invoke(sender, e);
    internal static void RaiseWebhooksUpdated(object? sender, WebhooksUpdateEvent e) => WebhooksUpdated?.Invoke(sender, e);
    internal static void RaiseInviteCreated(object? sender, InviteCreateEvent e) => InviteCreated?.Invoke(sender, e);
    internal static void RaiseInviteDeleted(object? sender, InviteDeleteEvent e) => InviteDeleted?.Invoke(sender, e);
    internal static void RaiseGuildIntegrationsUpdated(object? sender, GuildIntegrationsUpdateEvent e) => GuildIntegrationsUpdated?.Invoke(sender, e);

    internal static void RaiseDirectMessageReceived(object? sender, DirectMessageEvent e) => DirectMessageReceived?.Invoke(sender, e);

    internal static void RaiseAutoModerationRuleCreated(object? sender, AutoModerationRuleCreatedEvent e) => AutoModerationRuleCreated?.Invoke(sender, e);
    internal static void RaiseAutoModerationRuleUpdated(object? sender, AutoModerationRuleUpdatedEvent e) => AutoModerationRuleUpdated?.Invoke(sender, e);
    internal static void RaiseAutoModerationRuleDeleted(object? sender, AutoModerationRuleDeletedEvent e) => AutoModerationRuleDeleted?.Invoke(sender, e);
    internal static void RaiseAutoModerationActionExecution(object? sender, AutoModerationActionExecutionEvent e) => AutoModerationActionExecution?.Invoke(sender, e);

    internal static void RaiseStageInstanceCreated(object? sender, StageInstanceCreatedEvent e) => StageInstanceCreated?.Invoke(sender, e);
    internal static void RaiseStageInstanceUpdated(object? sender, StageInstanceUpdatedEvent e) => StageInstanceUpdated?.Invoke(sender, e);
    internal static void RaiseStageInstanceDeleted(object? sender, StageInstanceDeletedEvent e) => StageInstanceDeleted?.Invoke(sender, e);

    internal static void RaiseGuildScheduledEventCreated(object? sender, GuildScheduledEventCreatedEvent e) => GuildScheduledEventCreated?.Invoke(sender, e);
    internal static void RaiseGuildScheduledEventUpdated(object? sender, GuildScheduledEventUpdatedEvent e) => GuildScheduledEventUpdated?.Invoke(sender, e);
    internal static void RaiseGuildScheduledEventDeleted(object? sender, GuildScheduledEventDeletedEvent e) => GuildScheduledEventDeleted?.Invoke(sender, e);
    internal static void RaiseGuildScheduledEventUserAdded(object? sender, GuildScheduledEventUserAddedEvent e) => GuildScheduledEventUserAdded?.Invoke(sender, e);
    internal static void RaiseGuildScheduledEventUserRemoved(object? sender, GuildScheduledEventUserRemovedEvent e) => GuildScheduledEventUserRemoved?.Invoke(sender, e);

    internal static void RaiseIntegrationCreated(object? sender, IntegrationCreatedEvent e) => IntegrationCreated?.Invoke(sender, e);
    internal static void RaiseIntegrationUpdated(object? sender, IntegrationUpdatedEvent e) => IntegrationUpdated?.Invoke(sender, e);
    internal static void RaiseIntegrationDeleted(object? sender, IntegrationDeletedEvent e) => IntegrationDeleted?.Invoke(sender, e);

    internal static void RaiseVoiceServerUpdated(object? sender, VoiceServerUpdateEvent e) => VoiceServerUpdated?.Invoke(sender, e);

    internal static void RaiseGuildJoinRequestCreated(object? sender, GuildJoinRequestCreatedEvent e) => GuildJoinRequestCreated?.Invoke(sender, e);
    internal static void RaiseGuildJoinRequestUpdated(object? sender, GuildJoinRequestUpdatedEvent e) => GuildJoinRequestUpdated?.Invoke(sender, e);
    internal static void RaiseGuildJoinRequestDeleted(object? sender, GuildJoinRequestDeletedEvent e) => GuildJoinRequestDeleted?.Invoke(sender, e);

    internal static void RaisePollVoteAdded(object? sender, PollVoteAddedEvent e) => PollVoteAdded?.Invoke(sender, e);
    internal static void RaisePollVoteRemoved(object? sender, PollVoteRemovedEvent e) => PollVoteRemoved?.Invoke(sender, e);
}
