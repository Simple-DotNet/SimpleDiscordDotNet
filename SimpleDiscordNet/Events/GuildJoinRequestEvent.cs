using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Gateway payload for GUILD_JOIN_REQUEST_CREATE dispatch.
/// </summary>
public sealed record GuildJoinRequestCreatedEvent
{
    /// <summary>The guild ID where the join request was created.</summary>
    public required ulong GuildId { get; init; }
    
    /// <summary>The ID of the user who created the join request.</summary>
    public required ulong UserId { get; init; }
    
    /// <summary>The status of the join request.</summary>
    public required string Status { get; init; }
}

/// <summary>
/// Gateway payload for GUILD_JOIN_REQUEST_UPDATE dispatch.
/// </summary>
public sealed record GuildJoinRequestUpdatedEvent
{
    /// <summary>The guild ID where the join request was updated.</summary>
    public required ulong GuildId { get; init; }
    
    /// <summary>The ID of the user whose join request was updated.</summary>
    public required ulong UserId { get; init; }
    
    /// <summary>The updated status of the join request.</summary>
    public required string Status { get; init; }
}

/// <summary>
/// Gateway payload for GUILD_JOIN_REQUEST_DELETE dispatch.
/// </summary>
public sealed record GuildJoinRequestDeletedEvent
{
    /// <summary>The guild ID where the join request was deleted.</summary>
    public required ulong GuildId { get; init; }
    
    /// <summary>The ID of the user whose join request was deleted.</summary>
    public required ulong UserId { get; init; }
}