using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Payload for member-related events, including the member, user, and guild.
/// Contains point-in-time data captured when the event occurred.
/// </summary>
public sealed record MemberEvent
{
    /// <summary>
    /// The member entity from cache at the time of the event.
    /// Represents a point-in-time view captured when the event occurred.
    /// </summary>
    public required DiscordMember Member { get; init; }

    /// <summary>
    /// The user entity from cache at the time of the event.
    /// Represents a point-in-time view captured when the event occurred.
    /// </summary>
    public required DiscordUser User { get; init; }

    /// <summary>
    /// The guild entity from cache at the time of the event.
    /// Represents a point-in-time view captured when the event occurred.
    /// </summary>
    public required DiscordGuild Guild { get; init; }

    /// <summary>
    /// The previous state of the member before the update.
    /// Only populated for MemberUpdated events when the member was previously cached.
    /// Null for MemberJoined and MemberLeft events.
    /// </summary>
    public DiscordMember? BeforeUpdate { get; init; }

    /// <summary>Returns true if this is an update event with before state available.</summary>
    public bool HasBeforeUpdate => BeforeUpdate is not null;
}
