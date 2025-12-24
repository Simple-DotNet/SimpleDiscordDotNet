using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Internal gateway payload for member + guild id dispatches.
/// </summary>
internal sealed record GatewayMemberEvent
{
    public required ulong GuildId { get; init; }
    public required DiscordMember Member { get; init; }
}
