namespace SimpleDiscordNet.Entities;

/// <summary>
/// A poll attached to a message.
/// </summary>
public sealed record DiscordPoll
{
    public required PollMedia Question { get; init; }
    public PollAnswer[] Answers { get; init; } = [];

    /// <summary>When the poll ends.</summary>
    public DateTimeOffset? Expiry { get; init; }

    public bool Allow_Multiselect { get; init; }

    /// <summary>Vote counts. Null when Discord didn't include them, which means unknown rather than no votes.</summary>
    public PollResults? Results { get; init; }
}

/// <summary>
/// The text and optional emoji of a poll question or answer.
/// </summary>
public sealed record PollMedia
{
    public string? Text { get; init; }
    public DiscordEmoji? Emoji { get; init; }
}

/// <summary>
/// One answer in a poll.
/// </summary>
public sealed record PollAnswer
{
    public int Answer_Id { get; init; }
    public required PollMedia Poll_Media { get; init; }
}

/// <summary>
/// Vote counts for a poll.
/// </summary>
public sealed record PollResults
{
    /// <summary>True once Discord has finished the final count, after the poll ends.</summary>
    public bool Is_Finalized { get; init; }

    /// <summary>Counts per answer. An answer with no entry has no votes.</summary>
    public PollAnswerCount[] Answer_Counts { get; init; } = [];
}

/// <summary>
/// The vote count for one poll answer.
/// </summary>
public sealed record PollAnswerCount
{
    /// <summary>The answer's <see cref="PollAnswer.Answer_Id"/>.</summary>
    public int Id { get; init; }
    public int Count { get; init; }
    public bool Me_Voted { get; init; }
}
