using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Gateway payload for AUTO_MODERATION_RULE_CREATE dispatch.
/// </summary>
public sealed record AutoModerationRuleCreatedEvent
{
    /// <summary>The guild ID where the rule was created.</summary>
    public required ulong GuildId { get; init; }
    
    /// <summary>The ID of the auto moderation rule.</summary>
    public required ulong RuleId { get; init; }
    
    /// <summary>The rule data (partial).</summary>
    public object? Rule { get; init; }
}

/// <summary>
/// Gateway payload for AUTO_MODERATION_RULE_UPDATE dispatch.
/// </summary>
public sealed record AutoModerationRuleUpdatedEvent
{
    /// <summary>The guild ID where the rule was updated.</summary>
    public required ulong GuildId { get; init; }
    
    /// <summary>The ID of the auto moderation rule.</summary>
    public required ulong RuleId { get; init; }
    
    /// <summary>The updated rule data (partial).</summary>
    public object? Rule { get; init; }
}

/// <summary>
/// Gateway payload for AUTO_MODERATION_RULE_DELETE dispatch.
/// </summary>
public sealed record AutoModerationRuleDeletedEvent
{
    /// <summary>The guild ID where the rule was deleted.</summary>
    public required ulong GuildId { get; init; }
    
    /// <summary>The ID of the auto moderation rule.</summary>
    public required ulong RuleId { get; init; }
    
    /// <summary>The rule data before deletion (partial).</summary>
    public object? Rule { get; init; }
}

/// <summary>
/// Gateway payload for AUTO_MODERATION_ACTION_EXECUTION dispatch.
/// </summary>
public sealed record AutoModerationActionExecutionEvent
{
    /// <summary>The guild ID where the action was executed.</summary>
    public required ulong GuildId { get; init; }
    
    /// <summary>The ID of the auto moderation rule that triggered the action.</summary>
    public required ulong RuleId { get; init; }
    
    /// <summary>The action that was executed.</summary>
    public object? Action { get; init; }
    
    /// <summary>The ID of the user who triggered the action.</summary>
    public required ulong UserId { get; init; }
    
    /// <summary>The ID of the channel where the action was triggered.</summary>
    public required ulong ChannelId { get; init; }
    
    /// <summary>The ID of the message that triggered the action (if any).</summary>
    public ulong? MessageId { get; init; }
}