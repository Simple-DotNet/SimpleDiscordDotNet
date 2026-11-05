namespace SimpleDiscordNet.Entities;

/// <summary>
/// Represents a Discord message.
/// </summary>
public sealed record DiscordMessage
{
    public required ulong Id { get; init; }
    public required ulong ChannelId { get; init; }
    public ulong? GuildId { get; init; }
    public required DiscordUser Author { get; init; }
    public required string Content { get; init; }
    public DateTimeOffset? Timestamp { get; init; }
    public DateTimeOffset? Edited_Timestamp { get; init; }
    public bool Tts { get; init; }
    public bool Mention_Everyone { get; init; }
    public DiscordUser[]? Mentions { get; init; }
    public ulong[]? Mention_Roles { get; init; }
    public Attachment[]? Attachments { get; init; }
    public SimpleDiscordNet.Models.Embed[]? Embeds { get; init; }
    public Reaction[]? Reactions { get; init; }
    public bool Pinned { get; init; }
    public int Type { get; init; }

    /// <summary>The poll on this message, if any. Messages fetched through REST include this.</summary>
    public DiscordPoll? Poll { get; init; }

    /// <summary>Message type constants</summary>
    public static class MessageType
    {
        public const int Default = 0;
        public const int RecipientAdd = 1;
        public const int RecipientRemove = 2;
        public const int Call = 3;
        public const int ChannelNameChange = 4;
        public const int ChannelIconChange = 5;
        public const int ChannelPinnedMessage = 6;
        public const int UserJoin = 7;
        public const int GuildBoost = 8;
        public const int GuildBoostTier1 = 9;
        public const int GuildBoostTier2 = 10;
        public const int GuildBoostTier3 = 11;
        public const int ChannelFollowAdd = 12;
        public const int GuildDiscoveryDisqualified = 14;
        public const int GuildDiscoveryRequalified = 15;
        public const int Reply = 19;
        public const int ChatInputCommand = 20;
        public const int ThreadStarterMessage = 21;
        public const int ContextMenuCommand = 23;
        public const int AutoModerationAction = 24;
    }

    /// <summary>
    /// Pins this message in its channel.
    /// Example: await message.PinAsync();
    /// </summary>
    public Task PinAsync(CancellationToken ct = default)
        => Context.DiscordContext.Operations.PinMessageAsync(ChannelId, Id, ct);

    /// <summary>
    /// Unpins this message from its channel.
    /// Example: await message.UnpinAsync();
    /// </summary>
    public Task UnpinAsync(CancellationToken ct = default)
        => Context.DiscordContext.Operations.UnpinMessageAsync(ChannelId, Id, ct);

    /// <summary>
    /// Starts a thread from this message.
    /// Example: var thread = await message.CreateThreadAsync("Discussion");
    /// </summary>
    /// <param name="name">Thread name</param>
    /// <param name="autoArchiveMinutes">Minutes of inactivity before archiving: 60, 1440, 4320 or 10080</param>
    public Task<DiscordChannel?> CreateThreadAsync(string name, int? autoArchiveMinutes = null, CancellationToken ct = default)
        => Context.DiscordContext.Operations.CreateThreadFromMessageAsync(ChannelId, Id, name, autoArchiveMinutes, ct);

    /// <summary>
    /// Forwards this message to another channel. The bot must be able to read the message.
    /// Example: await message.ForwardAsync(logChannelId);
    /// </summary>
    public Task<DiscordMessage?> ForwardAsync(ulong toChannelId, CancellationToken ct = default)
        => Context.DiscordContext.Operations.ForwardMessageAsync(toChannelId, ChannelId, Id, ct);

    /// <summary>
    /// Ends this message's poll early and returns the updated message.
    /// Example: await message.EndPollAsync();
    /// </summary>
    public Task<DiscordMessage?> EndPollAsync(CancellationToken ct = default)
        => Context.DiscordContext.Operations.EndPollAsync(ChannelId, Id, ct);

    /// <summary>
    /// Gets everyone who voted for a poll answer.
    /// Example: var voters = await message.GetPollVotersAsync(message.Poll!.Answers[0].Answer_Id);
    /// </summary>
    public Task<IEnumerable<DiscordUser>> GetPollVotersAsync(int answerId, CancellationToken ct = default)
        => Context.DiscordContext.Operations.GetPollVotersAsync(ChannelId, Id, answerId, ct);

    /// <summary>
    /// Deletes this message.
    /// Example: await message.DeleteAsync();
    /// </summary>
    public Task DeleteAsync(CancellationToken ct = default)
        => Context.DiscordContext.Operations.DeleteMessageAsync(ChannelId, Id, ct);
}

/// <summary>
/// Represents a message attachment (file, image, etc.)
/// </summary>
public sealed record Attachment
{
    public required string Id { get; init; }
    public required string Filename { get; init; }
    public string? Description { get; init; }
    public string? Content_Type { get; init; }
    public required int Size { get; init; }
    public required string Url { get; init; }
    public string? Proxy_Url { get; init; }
    public int? Height { get; init; }
    public int? Width { get; init; }
    public bool? Ephemeral { get; init; }
}

/// <summary>
/// Represents a reaction on a message
/// </summary>
public sealed record Reaction
{
    public required int Count { get; init; }
    public required bool Me { get; init; }
    public required DiscordEmoji Emoji { get; init; }
}
