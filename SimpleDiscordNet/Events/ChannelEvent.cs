using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Payload for channel-related events, including the channel and its guild.
/// Contains point-in-time data captured when the event occurred.
/// </summary>
public sealed record ChannelEvent
{
    /// <summary>
    /// The channel entity from cache at the time of the event.
    /// Represents a point-in-time view captured when the event occurred.
    /// </summary>
    public required DiscordChannel Channel { get; init; }

    /// <summary>
    /// The guild entity from cache at the time of the event.
    /// Represents a point-in-time view captured when the event occurred.
    /// </summary>
    public required DiscordGuild Guild { get; init; }

    /// <summary>
    /// The previous state of the channel before the update.
    /// Only populated for ChannelUpdated events when the channel was previously cached.
    /// Null for ChannelCreated and ChannelDeleted events.
    /// </summary>
    public DiscordChannel? BeforeUpdate { get; init; }

    /// <summary>Returns true if this is an update event with before state available.</summary>
    public bool HasBeforeUpdate => BeforeUpdate is not null;
}
