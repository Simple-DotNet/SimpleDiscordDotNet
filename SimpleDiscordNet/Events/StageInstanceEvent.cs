using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Gateway payload for STAGE_INSTANCE_CREATE dispatch.
/// </summary>
public sealed record StageInstanceCreatedEvent
{
    /// <summary>The guild ID where the stage instance was created.</summary>
    public required ulong GuildId { get; init; }
    
    /// <summary>The ID of the stage instance.</summary>
    public required ulong StageInstanceId { get; init; }
    
    /// <summary>The channel ID where the stage instance is located.</summary>
    public required ulong ChannelId { get; init; }
    
    /// <summary>The topic of the stage instance.</summary>
    public string? Topic { get; init; }
    
    /// <summary>The privacy level of the stage instance.</summary>
    public int PrivacyLevel { get; init; }
}

/// <summary>
/// Gateway payload for STAGE_INSTANCE_UPDATE dispatch.
/// </summary>
public sealed record StageInstanceUpdatedEvent
{
    /// <summary>The guild ID where the stage instance was updated.</summary>
    public required ulong GuildId { get; init; }
    
    /// <summary>The ID of the stage instance.</summary>
    public required ulong StageInstanceId { get; init; }
    
    /// <summary>The channel ID where the stage instance is located.</summary>
    public required ulong ChannelId { get; init; }
    
    /// <summary>The updated topic of the stage instance.</summary>
    public string? Topic { get; init; }
    
    /// <summary>The updated privacy level of the stage instance.</summary>
    public int PrivacyLevel { get; init; }
}

/// <summary>
/// Gateway payload for STAGE_INSTANCE_DELETE dispatch.
/// </summary>
public sealed record StageInstanceDeletedEvent
{
    /// <summary>The guild ID where the stage instance was deleted.</summary>
    public required ulong GuildId { get; init; }
    
    /// <summary>The ID of the stage instance.</summary>
    public required ulong StageInstanceId { get; init; }
    
    /// <summary>The channel ID where the stage instance was located.</summary>
    public required ulong ChannelId { get; init; }
    
    /// <summary>The topic of the stage instance before deletion.</summary>
    public string? Topic { get; init; }
    
    /// <summary>The privacy level of the stage instance before deletion.</summary>
    public int PrivacyLevel { get; init; }
}