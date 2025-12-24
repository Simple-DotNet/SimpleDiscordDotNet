using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Internal gateway payload for GUILD_CREATE dispatch.
/// Includes the guild along with channels, members, and threads.
/// </summary>
internal sealed record GuildCreateEvent
{
    public required DiscordGuild Guild { get; init; }
    public DiscordChannel[]? Channels { get; init; }
    public DiscordMember[]? Members { get; init; }
    public DiscordChannel[]? Threads { get; init; }
}
