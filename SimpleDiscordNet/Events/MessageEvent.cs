using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Payload for message delete events.
/// Contains point-in-time data captured when the event occurred.
/// </summary>
public sealed record MessageEvent
{
    public required ulong MessageId { get; init; }
    public required ulong ChannelId { get; init; }
    public ulong? GuildId { get; init; }

    /// <summary>
    /// The guild entity from cache at the time of the event, if available and this was a guild message.
    /// Represents a point-in-time view captured when the event occurred.
    /// </summary>
    public DiscordGuild? Guild { get; init; }

    /// <summary>
    /// The channel entity from cache at the time of the event, if available.
    /// Represents a point-in-time view captured when the event occurred.
    /// </summary>
    public DiscordChannel? Channel { get; init; }

    /// <summary>
    /// The message that was deleted, if it was previously cached.
    /// For bulk deletes, this will be null.
    /// Note: Messages are not typically cached long-term, so this is often null.
    /// </summary>
    public DiscordMessage? DeletedMessage { get; init; }
}

/// <summary>
/// Payload for message update events.
/// Contains point-in-time data captured when the event occurred.
/// Discord only sends partial data for message updates, so Before may not have complete data.
/// </summary>
public sealed record MessageUpdateEvent
{
    public required ulong MessageId { get; init; }
    public required ulong ChannelId { get; init; }
    public ulong? GuildId { get; init; }

    /// <summary>
    /// The guild entity from cache at the time of the event, if available and this is a guild message.
    /// Represents a point-in-time view captured when the event occurred.
    /// </summary>
    public DiscordGuild? Guild { get; init; }

    /// <summary>
    /// The channel entity from cache at the time of the event, if available.
    /// Represents a point-in-time view captured when the event occurred.
    /// </summary>
    public DiscordChannel? Channel { get; init; }

    /// <summary>Updated content, if changed. Discord sends partial updates.</summary>
    public string? Content { get; init; }

    /// <summary>Edited timestamp.</summary>
    public DateTimeOffset? EditedTimestamp { get; init; }

    /// <summary>
    /// The previous state of the message before the update, if it was previously cached.
    /// Note: Discord doesn't cache all messages, so this may be null even if the message existed.
    /// </summary>
    public DiscordMessage? BeforeUpdate { get; init; }

    /// <summary>Returns true if we have the before state available.</summary>
    public bool HasBeforeUpdate => BeforeUpdate is not null;
}

/// <summary>
/// Payload for reaction events.
/// Contains point-in-time data captured when the event occurred.
/// </summary>
public sealed record ReactionEvent
{
    public required ulong MessageId { get; init; }
    public required ulong ChannelId { get; init; }
    public ulong? GuildId { get; init; }
    public required ulong UserId { get; init; }
    public required DiscordEmoji Emoji { get; init; }

    /// <summary>
    /// The guild entity from cache at the time of the event, if available and this is a guild message.
    /// Represents a point-in-time view captured when the event occurred.
    /// </summary>
    public DiscordGuild? Guild { get; init; }

    /// <summary>
    /// The channel entity from cache at the time of the event, if available.
    /// Represents a point-in-time view captured when the event occurred.
    /// </summary>
    public DiscordChannel? Channel { get; init; }
}
