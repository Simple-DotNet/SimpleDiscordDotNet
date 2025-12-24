using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Payload for bot user update events.
/// Contains point-in-time data captured when the event occurred.
/// </summary>
public sealed record BotUserEvent
{
    /// <summary>
    /// The user entity from cache at the time of the event.
    /// Represents a point-in-time view captured when the event occurred.
    /// </summary>
    public required DiscordUser User { get; init; }

    /// <summary>
    /// The previous state of the user before the update, if available in cache.
    /// Represents a point-in-time view captured before the update occurred.
    /// </summary>
    public DiscordUser? BeforeUpdate { get; init; }

    /// <summary>Returns true if we have the before state available.</summary>
    public bool HasBeforeUpdate => BeforeUpdate is not null;
}
