using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Gateway payload for VOICE_SERVER_UPDATE dispatch.
/// </summary>
public sealed record VoiceServerUpdateEvent
{
    /// <summary>The guild ID where the voice server was updated.</summary>
    public required ulong GuildId { get; init; }
    
    /// <summary>The voice server endpoint URL.</summary>
    public required string Endpoint { get; init; }
    
    /// <summary>The authentication token for the voice server.</summary>
    public required string Token { get; init; }
}