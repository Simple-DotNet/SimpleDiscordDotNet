using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Payload for guild-related events.
/// Contains point-in-time data captured when the event occurred.
/// </summary>
public sealed record GuildEvent
{
    /// <summary>
    /// The guild entity from cache at the time of the event.
    /// Represents a point-in-time view captured when the event occurred.
    /// </summary>
    public required DiscordGuild Guild { get; init; }

    /// <summary>
    /// The previous state of the guild before the update.
    /// Only populated for GuildUpdated events when the guild was previously cached.
    /// Null for GuildAdded and GuildRemoved events.
    /// </summary>
    public DiscordGuild? BeforeUpdate { get; init; }

    /// <summary>Returns true if this is an update event with before state available.</summary>
    public bool HasBeforeUpdate => BeforeUpdate is not null;
}
