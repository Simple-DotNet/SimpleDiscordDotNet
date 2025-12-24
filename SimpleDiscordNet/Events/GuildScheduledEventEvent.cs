using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Gateway payload for GUILD_SCHEDULED_EVENT_CREATE dispatch.
/// </summary>
public sealed record GuildScheduledEventCreatedEvent
{
    /// <summary>The guild ID where the scheduled event was created.</summary>
    public required ulong GuildId { get; init; }
    
    /// <summary>The ID of the scheduled event.</summary>
    public required ulong EventId { get; init; }
    
    /// <summary>The name of the scheduled event.</summary>
    public required string Name { get; init; }
    
    /// <summary>The description of the scheduled event.</summary>
    public string? Description { get; init; }
    
    /// <summary>The channel ID where the event will take place (if applicable).</summary>
    public ulong? ChannelId { get; init; }
    
    /// <summary>The start time of the scheduled event.</summary>
    public required DateTimeOffset StartTime { get; init; }
    
    /// <summary>The end time of the scheduled event (if applicable).</summary>
    public DateTimeOffset? EndTime { get; init; }
}

/// <summary>
/// Gateway payload for GUILD_SCHEDULED_EVENT_UPDATE dispatch.
/// </summary>
public sealed record GuildScheduledEventUpdatedEvent
{
    /// <summary>The guild ID where the scheduled event was updated.</summary>
    public required ulong GuildId { get; init; }
    
    /// <summary>The ID of the scheduled event.</summary>
    public required ulong EventId { get; init; }
    
    /// <summary>The updated name of the scheduled event.</summary>
    public required string Name { get; init; }
    
    /// <summary>The updated description of the scheduled event.</summary>
    public string? Description { get; init; }
    
    /// <summary>The updated channel ID where the event will take place (if applicable).</summary>
    public ulong? ChannelId { get; init; }
    
    /// <summary>The updated start time of the scheduled event.</summary>
    public required DateTimeOffset StartTime { get; init; }
    
    /// <summary>The updated end time of the scheduled event (if applicable).</summary>
    public DateTimeOffset? EndTime { get; init; }
}

/// <summary>
/// Gateway payload for GUILD_SCHEDULED_EVENT_DELETE dispatch.
/// </summary>
public sealed record GuildScheduledEventDeletedEvent
{
    /// <summary>The guild ID where the scheduled event was deleted.</summary>
    public required ulong GuildId { get; init; }
    
    /// <summary>The ID of the scheduled event.</summary>
    public required ulong EventId { get; init; }
    
    /// <summary>The name of the scheduled event before deletion.</summary>
    public required string Name { get; init; }
    
    /// <summary>The description of the scheduled event before deletion.</summary>
    public string? Description { get; init; }
    
    /// <summary>The channel ID where the event was scheduled (if applicable).</summary>
    public ulong? ChannelId { get; init; }
}

/// <summary>
/// Gateway payload for GUILD_SCHEDULED_EVENT_USER_ADD dispatch.
/// </summary>
public sealed record GuildScheduledEventUserAddedEvent
{
    /// <summary>The guild ID where the scheduled event is located.</summary>
    public required ulong GuildId { get; init; }
    
    /// <summary>The ID of the scheduled event.</summary>
    public required ulong EventId { get; init; }
    
    /// <summary>The ID of the user who was added to the event.</summary>
    public required ulong UserId { get; init; }
}

/// <summary>
/// Gateway payload for GUILD_SCHEDULED_EVENT_USER_REMOVE dispatch.
/// </summary>
public sealed record GuildScheduledEventUserRemovedEvent
{
    /// <summary>The guild ID where the scheduled event is located.</summary>
    public required ulong GuildId { get; init; }
    
    /// <summary>The ID of the scheduled event.</summary>
    public required ulong EventId { get; init; }
    
    /// <summary>The ID of the user who was removed from the event.</summary>
    public required ulong UserId { get; init; }
}