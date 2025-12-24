using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Gateway payload for TYPING_START dispatch.
/// </summary>
public sealed record TypingStartEvent
{
    /// <summary>The channel ID where the user started typing.</summary>
    public required ulong ChannelId { get; init; }
    
    /// <summary>The guild ID if typing occurred in a guild channel.</summary>
    public ulong? GuildId { get; init; }
    
    /// <summary>The user ID who started typing.</summary>
    public required ulong UserId { get; init; }
    
    /// <summary>Unix timestamp (in seconds) when the user started typing.</summary>
    public required int Timestamp { get; init; }
    
    /// <summary>Member object if typing occurred in a guild.</summary>
    public DiscordMember? Member { get; init; }
}