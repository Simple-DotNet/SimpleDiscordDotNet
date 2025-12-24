using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Gateway payload for WEBHOOKS_UPDATE dispatch.
/// </summary>
public sealed record WebhooksUpdateEvent
{
    /// <summary>The guild ID where webhooks were updated.</summary>
    public required ulong GuildId { get; init; }
    
    /// <summary>The channel ID where webhooks were updated.</summary>
    public required ulong ChannelId { get; init; }
}