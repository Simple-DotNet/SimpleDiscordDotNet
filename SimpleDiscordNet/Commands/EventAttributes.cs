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

/// <summary>Runs when the bot connects.</summary>
public sealed class OnConnectedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.Connected));

/// <summary>Runs when a guild's data has finished loading.</summary>
public sealed class OnGuildReadyAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.GuildReady));

/// <summary>Runs when the bot joins a guild.</summary>
public sealed class OnGuildAddedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.GuildAdded));

/// <summary>Runs when a message is sent.</summary>
public sealed class OnMessageCreatedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.MessageCreated));

/// <summary>Runs when a message is edited.</summary>
public sealed class OnMessageUpdatedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.MessageUpdated));

/// <summary>Runs when a message is deleted.</summary>
public sealed class OnMessageDeletedAttribute() : OnEventAttribute(nameof(Events.DiscordEvents.MessageDeleted));

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
