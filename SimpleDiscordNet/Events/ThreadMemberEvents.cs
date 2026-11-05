using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Raised when the bot's own membership of a thread changes.
/// </summary>
public sealed record ThreadMemberUpdateEvent
{
    public required ulong ThreadId { get; init; }
    public required ulong GuildId { get; init; }
    public required ulong UserId { get; init; }
}

/// <summary>
/// Raised when people are added to or removed from a thread.
/// Without the GuildMembers intent, this only fires when the bot itself was added or removed.
/// </summary>
public sealed record ThreadMembersUpdateEvent
{
    public required ulong ThreadId { get; init; }
    public required ulong GuildId { get; init; }

    /// <summary>Approximate member count, capped at 50 by Discord.</summary>
    public int MemberCount { get; init; }

    public ulong[] AddedUserIds { get; init; } = [];
    public ulong[] RemovedUserIds { get; init; } = [];
}

/// <summary>
/// Raised when the bot gains access to a channel and receives its active threads.
/// </summary>
public sealed record ThreadListSyncEvent
{
    public required ulong GuildId { get; init; }

    /// <summary>The parent channels that were synced. Null means the entire guild.</summary>
    public ulong[]? ChannelIds { get; init; }

    public DiscordChannel[] Threads { get; init; } = [];
}
