using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Gateway payload for INVITE_CREATE dispatch.
/// </summary>
public sealed record InviteCreateEvent
{
    /// <summary>The channel ID where the invite was created.</summary>
    public required ulong ChannelId { get; init; }
    
    /// <summary>The guild ID where the invite was created.</summary>
    public required ulong GuildId { get; init; }
    
    /// <summary>The invite code.</summary>
    public required string Code { get; init; }
    
    /// <summary>The user who created the invite.</summary>
    public required DiscordUser Inviter { get; init; }
    
    /// <summary>Number of times the invite can be used.</summary>
    public int? MaxUses { get; init; }
    
    /// <summary>Duration in seconds after which the invite expires.</summary>
    public int? MaxAge { get; init; }
    
    /// <summary>Whether the invite is temporary (invited users kicked after disconnect).</summary>
    public bool Temporary { get; init; }
    
    /// <summary>When the invite was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Gateway payload for INVITE_DELETE dispatch.
/// </summary>
public sealed record InviteDeleteEvent
{
    /// <summary>The channel ID where the invite was deleted.</summary>
    public required ulong ChannelId { get; init; }
    
    /// <summary>The guild ID where the invite was deleted.</summary>
    public required ulong GuildId { get; init; }
    
    /// <summary>The invite code.</summary>
    public required string Code { get; init; }
}