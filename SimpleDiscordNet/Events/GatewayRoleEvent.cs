using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Internal gateway payload for role + guild id dispatches.
/// </summary>
internal sealed record GatewayRoleEvent
{
    public required ulong GuildId { get; init; }
    public required DiscordRole Role { get; init; }
}
