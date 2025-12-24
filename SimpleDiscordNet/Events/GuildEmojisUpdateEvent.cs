using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Public event payload for GUILD_EMOJIS_UPDATE dispatch.
/// </summary>
public sealed record GuildEmojisUpdateEvent
{
    public required ulong GuildId { get; init; }
    public required DiscordEmoji[] Emojis { get; init; }
}
