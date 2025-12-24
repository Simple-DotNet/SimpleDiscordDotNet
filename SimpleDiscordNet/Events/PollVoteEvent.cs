using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Gateway payload for MESSAGE_POLL_VOTE_ADD dispatch.
/// </summary>
public sealed record PollVoteAddedEvent
{
    /// <summary>The guild ID where the vote was added (if any).</summary>
    public ulong? GuildId { get; init; }
    
    /// <summary>The channel ID where the vote was added.</summary>
    public required ulong ChannelId { get; init; }
    
    /// <summary>The message ID containing the poll.</summary>
    public required ulong MessageId { get; init; }
    
    /// <summary>The ID of the user who voted.</summary>
    public required ulong UserId { get; init; }
    
    /// <summary>The ID of the poll answer that was voted for.</summary>
    public required ulong AnswerId { get; init; }
}

/// <summary>
/// Gateway payload for MESSAGE_POLL_VOTE_REMOVE dispatch.
/// </summary>
public sealed record PollVoteRemovedEvent
{
    /// <summary>The guild ID where the vote was removed (if any).</summary>
    public ulong? GuildId { get; init; }
    
    /// <summary>The channel ID where the vote was removed.</summary>
    public required ulong ChannelId { get; init; }
    
    /// <summary>The message ID containing the poll.</summary>
    public required ulong MessageId { get; init; }
    
    /// <summary>The ID of the user who removed their vote.</summary>
    public required ulong UserId { get; init; }
    
    /// <summary>The ID of the poll answer that was removed.</summary>
    public required ulong AnswerId { get; init; }
}