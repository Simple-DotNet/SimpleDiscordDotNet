using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Gateway payload for VOICE_STATE_UPDATE dispatch.
/// </summary>
public sealed record VoiceStateUpdateEvent
{
    /// <summary>The guild ID where the voice state was updated.</summary>
    public required ulong GuildId { get; init; }
    
    /// <summary>The channel ID where the user is connected (null if disconnected).</summary>
    public ulong? ChannelId { get; init; }
    
    /// <summary>The user ID whose voice state changed.</summary>
    public required ulong UserId { get; init; }
    
    /// <summary>Session ID for the voice connection.</summary>
    public required string SessionId { get; init; }
    
    /// <summary>Whether the user is server deafened.</summary>
    public bool Deaf { get; init; }
    
    /// <summary>Whether the user is server muted.</summary>
    public bool Mute { get; init; }
    
    /// <summary>Whether the user is self deafened.</summary>
    public bool SelfDeaf { get; init; }
    
    /// <summary>Whether the user is self muted.</summary>
    public bool SelfMute { get; init; }
    
    /// <summary>Whether the user is suppressed (priority speaker).</summary>
    public bool Suppress { get; init; }
    
    /// <summary>When the user requested to speak (nullable).</summary>
    public DateTimeOffset? RequestToSpeakTimestamp { get; init; }
}