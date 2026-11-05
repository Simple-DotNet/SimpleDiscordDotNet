namespace SimpleDiscordNet.Events;

/// <summary>
/// Raised when a message is pinned or unpinned. Discord doesn't send this when a pinned message is deleted.
/// </summary>
public sealed record ChannelPinsUpdateEvent
{
    public required ulong ChannelId { get; init; }

    /// <summary>The guild, or null in a DM.</summary>
    public ulong? GuildId { get; init; }

    /// <summary>When the most recent pin was made, or null if the channel has no pins left.</summary>
    public DateTimeOffset? LastPinTimestamp { get; init; }
}
