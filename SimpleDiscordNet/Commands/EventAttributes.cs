namespace SimpleDiscordNet.Commands;

/// <summary>
/// Runs a method when a <see cref="Events.DiscordEvents"/> event fires. The method takes the event's data
/// as its only parameter, or no parameters. It runs off the gateway's receive loop, so it can safely be slow.
/// There are shorter attributes for common events, such as [OnMemberJoined].
/// Example: [OnEvent(nameof(DiscordEvents.PollVoteAdded))]
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class OnEventAttribute(string eventName) : Attribute
{
    /// <summary>The name of the event on DiscordEvents.</summary>
    public string EventName { get; } = eventName;
}

// Each shortcut is named On + the event's name, which is how the source generator finds its event.

/// <summary>Runs when the bot connects.</summary>
public sealed class OnConnectedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.Connected));

/// <summary>Runs when the bot loses its connection. The library reconnects by itself.</summary>
public sealed class OnDisconnectedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.Disconnected));

/// <summary>Runs when the bot picks up its session again after a brief disconnect.</summary>
public sealed class OnSessionResumedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.SessionResumed));

/// <summary>Runs when something goes wrong in the library or in an event handler.</summary>
public sealed class OnErrorAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.Error));

/// <summary>Runs when a guild's data has finished loading.</summary>
public sealed class OnGuildReadyAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.GuildReady));

/// <summary>Runs when the bot joins a guild.</summary>
public sealed class OnGuildAddedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.GuildAdded));

/// <summary>Runs when a guild's settings change.</summary>
public sealed class OnGuildUpdatedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.GuildUpdated));

/// <summary>Runs when the bot leaves a guild. The method receives the guild's ID as a string.</summary>
public sealed class OnGuildRemovedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.GuildRemoved));

/// <summary>Runs when a guild's emojis change.</summary>
public sealed class OnGuildEmojisUpdatedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.GuildEmojisUpdated));

/// <summary>Runs when a channel is created.</summary>
public sealed class OnChannelCreatedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.ChannelCreated));

/// <summary>Runs when a channel changes.</summary>
public sealed class OnChannelUpdatedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.ChannelUpdated));

/// <summary>Runs when a channel is deleted.</summary>
public sealed class OnChannelDeletedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.ChannelDeleted));

/// <summary>Runs when a message is pinned or unpinned.</summary>
public sealed class OnChannelPinsUpdatedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.ChannelPinsUpdated));

/// <summary>Runs when a role is created.</summary>
public sealed class OnRoleCreatedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.RoleCreated));

/// <summary>Runs when a role changes.</summary>
public sealed class OnRoleUpdatedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.RoleUpdated));

/// <summary>Runs when a role is deleted.</summary>
public sealed class OnRoleDeletedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.RoleDeleted));

/// <summary>Runs when a thread is created.</summary>
public sealed class OnThreadCreatedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.ThreadCreated));

/// <summary>Runs when a thread changes, such as being archived.</summary>
public sealed class OnThreadUpdatedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.ThreadUpdated));

/// <summary>Runs when a thread is deleted.</summary>
public sealed class OnThreadDeletedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.ThreadDeleted));

/// <summary>Runs when people join or leave a thread.</summary>
public sealed class OnThreadMembersUpdatedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.ThreadMembersUpdated));

/// <summary>Runs when a user is banned.</summary>
public sealed class OnBanAddedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.BanAdded));

/// <summary>Runs when a user is unbanned.</summary>
public sealed class OnBanRemovedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.BanRemoved));

/// <summary>Runs when a new audit log entry appears.</summary>
public sealed class OnAuditLogEntryCreatedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.AuditLogEntryCreated));

/// <summary>Runs when AutoMod takes action.</summary>
public sealed class OnAutoModerationActionExecutionAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.AutoModerationActionExecution));

/// <summary>Runs when someone joins, leaves or changes state in a voice channel.</summary>
public sealed class OnVoiceStateUpdatedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.VoiceStateUpdated));

/// <summary>Runs when someone's online status or activity changes.</summary>
public sealed class OnPresenceUpdatedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.PresenceUpdated));

/// <summary>Runs when someone starts typing.</summary>
public sealed class OnTypingStartedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.TypingStarted));

/// <summary>Runs when someone votes on a poll.</summary>
public sealed class OnPollVoteAddedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.PollVoteAdded));

/// <summary>Runs when someone removes their poll vote.</summary>
public sealed class OnPollVoteRemovedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.PollVoteRemoved));

/// <summary>Runs when an invite is created.</summary>
public sealed class OnInviteCreatedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.InviteCreated));

/// <summary>Runs when an invite is deleted.</summary>
public sealed class OnInviteDeletedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.InviteDeleted));

/// <summary>Runs when a scheduled event is created.</summary>
public sealed class OnGuildScheduledEventCreatedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.GuildScheduledEventCreated));

/// <summary>Runs when a scheduled event changes.</summary>
public sealed class OnGuildScheduledEventUpdatedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.GuildScheduledEventUpdated));

/// <summary>Runs when a scheduled event is deleted.</summary>
public sealed class OnGuildScheduledEventDeletedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.GuildScheduledEventDeleted));

/// <summary>Runs when a message is sent.</summary>
public sealed class OnMessageCreatedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.MessageCreated));

/// <summary>Runs when a message is edited.</summary>
public sealed class OnMessageUpdatedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.MessageUpdated));

/// <summary>Runs when a message is deleted.</summary>
public sealed class OnMessageDeletedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.MessageDeleted));

/// <summary>Runs when many messages are deleted at once.</summary>
public sealed class OnMessagesBulkDeletedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.MessagesBulkDeleted));

/// <summary>Runs when someone sends the bot a direct message.</summary>
public sealed class OnDirectMessageAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.DirectMessageReceived));

/// <summary>Runs when a member joins a guild.</summary>
public sealed class OnMemberJoinedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.MemberJoined));

/// <summary>Runs when a member leaves a guild.</summary>
public sealed class OnMemberLeftAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.MemberLeft));

/// <summary>Runs when a member changes.</summary>
public sealed class OnMemberUpdatedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.MemberUpdated));

/// <summary>Runs when a reaction is added.</summary>
public sealed class OnReactionAddedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.ReactionAdded));

/// <summary>Runs when a reaction is removed.</summary>
public sealed class OnReactionRemovedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.ReactionRemoved));

/// <summary>
/// Handles every exception from a command, button or modal handler, in place of the default error message.
/// The method takes an InteractionContext and an Exception. Use it once per app.
/// Example: [OnCommandError] public Task OnError(InteractionContext ctx, Exception ex) => ctx.RespondAsync("Sorry!");
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class OnCommandErrorAttribute : Attribute;

/// <summary>
/// Adds a command that lists your bot's slash commands, shown only to the user who runs it.
/// Example: [assembly: GenerateHelp(Title = "My Bot Commands")]
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class GenerateHelpAttribute : Attribute
{
    /// <summary>The command's name. Defaults to help.</summary>
    public string Name { get; set; } = "help";

    /// <summary>The command's description.</summary>
    public string Description { get; set; } = "List the bot's commands";

    /// <summary>The title of the list.</summary>
    public string Title { get; set; } = "Commands";
}
