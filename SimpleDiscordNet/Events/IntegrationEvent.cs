using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Gateway payload for INTEGRATION_CREATE dispatch.
/// </summary>
public sealed record IntegrationCreatedEvent
{
    /// <summary>The guild ID where the integration was created.</summary>
    public required ulong GuildId { get; init; }
    
    /// <summary>The ID of the integration.</summary>
    public required ulong IntegrationId { get; init; }
    
    /// <summary>The name of the integration.</summary>
    public required string Name { get; init; }
    
    /// <summary>The type of the integration (e.g., twitch, youtube).</summary>
    public required string Type { get; init; }
    
    /// <summary>Whether the integration is enabled.</summary>
    public bool Enabled { get; init; }
}

/// <summary>
/// Gateway payload for INTEGRATION_UPDATE dispatch.
/// </summary>
public sealed record IntegrationUpdatedEvent
{
    /// <summary>The guild ID where the integration was updated.</summary>
    public required ulong GuildId { get; init; }
    
    /// <summary>The ID of the integration.</summary>
    public required ulong IntegrationId { get; init; }
    
    /// <summary>The updated name of the integration.</summary>
    public required string Name { get; init; }
    
    /// <summary>The updated type of the integration.</summary>
    public required string Type { get; init; }
    
    /// <summary>Whether the integration is enabled.</summary>
    public bool Enabled { get; init; }
}

/// <summary>
/// Gateway payload for INTEGRATION_DELETE dispatch.
/// </summary>
public sealed record IntegrationDeletedEvent
{
    /// <summary>The guild ID where the integration was deleted.</summary>
    public required ulong GuildId { get; init; }
    
    /// <summary>The ID of the integration.</summary>
    public required ulong IntegrationId { get; init; }
    
    /// <summary>The application ID associated with the integration (if any).</summary>
    public ulong? ApplicationId { get; init; }
}