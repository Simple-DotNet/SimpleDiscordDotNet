using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Payload for thread-related events.
/// Contains point-in-time data captured when the event occurred.
/// </summary>
public sealed record ThreadEvent
{
    /// <summary>
    /// The thread (channel) entity from cache at the time of the event.
    /// Represents a point-in-time view captured when the event occurred.
    /// </summary>
    public required DiscordChannel Thread { get; init; }

    /// <summary>
    /// The guild entity from cache at the time of the event.
    /// Represents a point-in-time view captured when the event occurred.
    /// </summary>
    public required DiscordGuild Guild { get; init; }

    /// <summary>
    /// The previous state of the thread before the update.
    /// Only populated for ThreadUpdated events when the thread was previously cached.
    /// Null for ThreadCreated and ThreadDeleted events.
    /// </summary>
    public DiscordChannel? BeforeUpdate { get; init; }

    /// <summary>Returns true if this is an update event with before state available.</summary>
    public bool HasBeforeUpdate => BeforeUpdate is not null;
}
