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
    public static event EventHandler? Connected;
    public static event EventHandler<Exception?>? Disconnected;
    public static event EventHandler<Exception>? Error;

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
    public static event EventHandler<GuildEvent>? GuildAdded;
    public static event EventHandler<GuildEvent>? GuildUpdated;
    public static event EventHandler<string>? GuildRemoved; // guild id
    /// <summary>
    /// Fired when a guild is fully loaded with all members, channels, roles, and emojis.
    /// Only fires when AutoLoadFullGuildData is enabled (default).
    /// </summary>
    public static event EventHandler<GuildEvent>? GuildReady;

    public static event EventHandler<ChannelEvent>? ChannelCreated;
    public static event EventHandler<ChannelEvent>? ChannelUpdated;
    public static event EventHandler<ChannelEvent>? ChannelDeleted;

    public static event EventHandler<RoleEvent>? RoleCreated;
    public static event EventHandler<RoleEvent>? RoleUpdated;
    public static event EventHandler<RoleEvent>? RoleDeleted;

    public static event EventHandler<ThreadEvent>? ThreadCreated;
    public static event EventHandler<ThreadEvent>? ThreadUpdated;
    public static event EventHandler<ThreadEvent>? ThreadDeleted;

    public static event EventHandler<MessageCreateEvent>? MessageCreated;
    public static event EventHandler<MessageUpdateEvent>? MessageUpdated;
    public static event EventHandler<MessageEvent>? MessageDeleted;
    public static event EventHandler<MessageEvent>? MessagesBulkDeleted;

    public static event EventHandler<ReactionEvent>? ReactionAdded;
    public static event EventHandler<ReactionEvent>? ReactionRemoved;
    public static event EventHandler<ReactionEvent>? ReactionsClearedForEmoji;
    public static event EventHandler<MessageEvent>? ReactionsCleared;

    public static event EventHandler<MemberEvent>? MemberJoined;
    public static event EventHandler<MemberEvent>? MemberUpdated;
    public static event EventHandler<MemberEvent>? MemberLeft; // includes kicks/leaves

    public static event EventHandler<GuildMembersChunkEvent>? GuildMembersChunk;

    public static event EventHandler<BanEvent>? BanAdded;
    public static event EventHandler<BanEvent>? BanRemoved;

    public static event EventHandler<BotUserEvent>? BotUserUpdated; // Only the bot user per Discord API

    // Audit logs
    /// <summary>
    /// Fired when a new audit log entry is created in a guild.
    /// Requires GUILD_MODERATION intent.
    /// </summary>
    public static event EventHandler<AuditLogEvent>? AuditLogEntryCreated;

    // Interactions
    public static event EventHandler<InteractionCreateEvent>? InteractionCreated;

    // Guild emojis
    public static event EventHandler<GuildEmojisUpdateEvent>? GuildEmojisUpdated;

    // Voice
    public static event EventHandler<VoiceStateUpdateEvent>? VoiceStateUpdated;

    // Presence
    public static event EventHandler<PresenceUpdateEvent>? PresenceUpdated;

    // Typing
    public static event EventHandler<TypingStartEvent>? TypingStarted;

    // Webhooks
    public static event EventHandler<WebhooksUpdateEvent>? WebhooksUpdated;

    // Invites
    public static event EventHandler<InviteCreateEvent>? InviteCreated;
    public static event EventHandler<InviteDeleteEvent>? InviteDeleted;
    // Integrations
    public static event EventHandler<GuildIntegrationsUpdateEvent>? GuildIntegrationsUpdated;

    // Direct messages
    public static event EventHandler<DirectMessageEvent>? DirectMessageReceived;

    // Auto Moderation
    public static event EventHandler<AutoModerationRuleCreatedEvent>? AutoModerationRuleCreated;
    public static event EventHandler<AutoModerationRuleUpdatedEvent>? AutoModerationRuleUpdated;
    public static event EventHandler<AutoModerationRuleDeletedEvent>? AutoModerationRuleDeleted;
    public static event EventHandler<AutoModerationActionExecutionEvent>? AutoModerationActionExecution;

    // Stage Instance
    public static event EventHandler<StageInstanceCreatedEvent>? StageInstanceCreated;
    public static event EventHandler<StageInstanceUpdatedEvent>? StageInstanceUpdated;
    public static event EventHandler<StageInstanceDeletedEvent>? StageInstanceDeleted;

    // Guild Scheduled Event
    public static event EventHandler<GuildScheduledEventCreatedEvent>? GuildScheduledEventCreated;
    public static event EventHandler<GuildScheduledEventUpdatedEvent>? GuildScheduledEventUpdated;
    public static event EventHandler<GuildScheduledEventDeletedEvent>? GuildScheduledEventDeleted;
    public static event EventHandler<GuildScheduledEventUserAddedEvent>? GuildScheduledEventUserAdded;
    public static event EventHandler<GuildScheduledEventUserRemovedEvent>? GuildScheduledEventUserRemoved;

    // Integration
    public static event EventHandler<IntegrationCreatedEvent>? IntegrationCreated;
    public static event EventHandler<IntegrationUpdatedEvent>? IntegrationUpdated;
    public static event EventHandler<IntegrationDeletedEvent>? IntegrationDeleted;

    // Voice Server
    public static event EventHandler<VoiceServerUpdateEvent>? VoiceServerUpdated;

    // Guild Join Request
    public static event EventHandler<GuildJoinRequestCreatedEvent>? GuildJoinRequestCreated;
    public static event EventHandler<GuildJoinRequestUpdatedEvent>? GuildJoinRequestUpdated;
    public static event EventHandler<GuildJoinRequestDeletedEvent>? GuildJoinRequestDeleted;

    // Poll Vote
    public static event EventHandler<PollVoteAddedEvent>? PollVoteAdded;
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
