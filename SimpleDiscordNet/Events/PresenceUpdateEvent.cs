using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Gateway payload for PRESENCE_UPDATE dispatch.
/// </summary>
public sealed record PresenceUpdateEvent
{
    /// <summary>The guild ID where the presence was updated.</summary>
    public required ulong GuildId { get; init; }
    
    /// <summary>The user whose presence changed.</summary>
    public required DiscordUser User { get; init; }
    
    /// <summary>The user's new status (online, idle, dnd, offline).</summary>
    public required string Status { get; init; }
    
    /// <summary>The user's current activities.</summary>
    public Activity[] Activities { get; init; } = [];
    
    /// <summary>When the user started their current activity.</summary>
    public DateTimeOffset? Since { get; init; }
}

/// <summary>
/// Represents a Discord activity.
/// </summary>
public sealed record Activity
{
    /// <summary>Activity name.</summary>
    public required string Name { get; init; }
    
    /// <summary>Activity type (0-5).</summary>
    public int Type { get; init; }
    
    /// <summary>Stream URL, if type is streaming.</summary>
    public string? Url { get; init; }
    
    /// <summary>Timestamp when activity started.</summary>
    public DateTimeOffset? CreatedAt { get; init; }
}