using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Gateway payload for GUILD_INTEGRATIONS_UPDATE dispatch.
/// </summary>
public sealed record GuildIntegrationsUpdateEvent
{
    /// <summary>The guild ID where integrations were updated.</summary>
    public required ulong GuildId { get; init; }
}