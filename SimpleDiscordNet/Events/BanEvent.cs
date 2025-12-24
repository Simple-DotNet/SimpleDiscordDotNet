using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Payload for ban-related events.
/// Contains point-in-time data captured when the event occurred.
/// </summary>
public sealed record BanEvent
{
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
    /// The member entity from cache at the time of the event, if they were cached before the ban.
    /// Represents a point-in-time view captured when the event occurred.
    /// </summary>
    public DiscordMember? Member { get; init; }
}
