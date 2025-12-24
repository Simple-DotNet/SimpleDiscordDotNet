using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Models;

/// <summary>
/// Event raised when a message is created in any channel (guild or DM).
/// Contains point-in-time data captured when the event occurred.
/// </summary>
public sealed record MessageCreateEvent
{
    public required ulong Id { get; init; }
    public required DiscordChannel Channel { get; init; }
    public DiscordGuild? Guild { get; init; }
    public required string Content { get; init; }
    public required DiscordUser Author { get; init; }
}

/// <summary>
/// Raw gateway event model - used internally but exposed in DM contexts.
/// </summary>
public sealed record MessageCreateEventRaw
{
    public required string Id { get; init; }
    public required string ChannelId { get; init; }
    public string? GuildId { get; init; }
    public required string Content { get; init; }
    public required Author Author { get; init; }
}

/// <summary>
/// Minimal author information from gateway events.
/// </summary>
public sealed record Author
{
    public required ulong Id { get; init; }
    public required string Username { get; init; }
}
