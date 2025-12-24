using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Internal gateway payload for user + guild id dispatches.
/// </summary>
internal sealed record GatewayUserEvent
{
    public required ulong GuildId { get; init; }
    public required DiscordUser User { get; init; }
}
