using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Payload for role-related events.
/// Contains point-in-time data captured when the event occurred.
/// </summary>
public sealed record RoleEvent
{
    /// <summary>
    /// The role entity from cache at the time of the event.
    /// Represents a point-in-time view captured when the event occurred.
    /// </summary>
    public required DiscordRole Role { get; init; }

    /// <summary>
    /// The guild entity from cache at the time of the event.
    /// Represents a point-in-time view captured when the event occurred.
    /// </summary>
    public required DiscordGuild Guild { get; init; }

    /// <summary>
    /// The previous state of the role before the update.
    /// Only populated for RoleUpdated events when the role was previously cached.
    /// Null for RoleCreated and RoleDeleted events.
    /// </summary>
    public DiscordRole? BeforeUpdate { get; init; }

    /// <summary>Returns true if this is an update event with before state available.</summary>
    public bool HasBeforeUpdate => BeforeUpdate is not null;
}
