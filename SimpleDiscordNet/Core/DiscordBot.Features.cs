using System.Globalization;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Events;
using SimpleDiscordNet.Gateway;
using SimpleDiscordNet.Logging;
using SimpleDiscordNet.Models.Requests;
using SimpleDiscordNet.Primitives;
using SimpleDiscordNet.Sharding;

namespace SimpleDiscordNet;

public sealed partial class DiscordBot
{
    private const int PublicThreadType = 11;
    private const int PrivateThreadType = 12;
    private const int ForwardReferenceType = 1;
    private const int MaxBulkBanUsers = 200;
    private const int SearchIndexRetries = 3;

    private string? _applicationId;

    private static string ToId(ulong id) => id.ToString(CultureInfo.InvariantCulture);

    // ---- Threads and forum posts ----

    /// <summary>
    /// Creates a thread in a channel without a starting message.
    /// Example: var thread = await bot.CreateThreadAsync(channelId, "Planning");
    /// </summary>
    /// <param name="channelId">The text or announcement channel</param>
    /// <param name="name">Thread name</param>
    /// <param name="isPrivate">True for a private thread that's invite-only</param>
    /// <param name="autoArchiveMinutes">Minutes of inactivity before archiving: 60, 1440, 4320 or 10080</param>
    public Task<DiscordChannel?> CreateThreadAsync(string channelId, string name, bool isPrivate = false, int? autoArchiveMinutes = null, CancellationToken ct = default)
    {
        StartThreadRequest request = new()
        {
            name = name,
            auto_archive_duration = autoArchiveMinutes,
            type = isPrivate ? PrivateThreadType : PublicThreadType
        };
        return _rest.PostAsync<DiscordChannel>($"/channels/{channelId}/threads", request, ct);
    }

    /// <inheritdoc cref="CreateThreadAsync(string, string, bool, int?, CancellationToken)"/>
    public Task<DiscordChannel?> CreateThreadAsync(ulong channelId, string name, bool isPrivate = false, int? autoArchiveMinutes = null, CancellationToken ct = default)
        => CreateThreadAsync(ToId(channelId), name, isPrivate, autoArchiveMinutes, ct);

    /// <summary>
    /// Starts a thread from an existing message.
    /// Example: var thread = await bot.CreateThreadFromMessageAsync(channelId, messageId, "Discussion");
    /// </summary>
    public Task<DiscordChannel?> CreateThreadFromMessageAsync(string channelId, string messageId, string name, int? autoArchiveMinutes = null, CancellationToken ct = default)
    {
        StartThreadRequest request = new() { name = name, auto_archive_duration = autoArchiveMinutes };
        return _rest.PostAsync<DiscordChannel>($"/channels/{channelId}/messages/{messageId}/threads", request, ct);
    }

    /// <inheritdoc cref="CreateThreadFromMessageAsync(string, string, string, int?, CancellationToken)"/>
    public Task<DiscordChannel?> CreateThreadFromMessageAsync(ulong channelId, ulong messageId, string name, int? autoArchiveMinutes = null, CancellationToken ct = default)
        => CreateThreadFromMessageAsync(ToId(channelId), ToId(messageId), name, autoArchiveMinutes, ct);

    /// <summary>
    /// Creates a post in a forum or media channel. The returned message's ChannelId is the new post's thread.
    /// To apply tags, use MessageBuilder.WithAppliedTags.
    /// Example: await bot.CreateForumPostAsync(forumId, "Patch notes", new MessageBuilder().WithContent("Version 2 is out!"));
    /// </summary>
    public Task<DiscordMessage?> CreateForumPostAsync(string channelId, string title, MessageBuilder message, int? autoArchiveMinutes = null, CancellationToken ct = default)
        => PostForumThreadAsync(channelId, title, message.Build(), message.GetFiles(), autoArchiveMinutes, ct);

    /// <inheritdoc cref="CreateForumPostAsync(string, string, MessageBuilder, int?, CancellationToken)"/>
    public Task<DiscordMessage?> CreateForumPostAsync(ulong channelId, string title, MessageBuilder message, int? autoArchiveMinutes = null, CancellationToken ct = default)
        => CreateForumPostAsync(ToId(channelId), title, message, autoArchiveMinutes, ct);

    private async Task<DiscordMessage?> PostForumThreadAsync(string channelId, string title, MessagePayload payload,
        List<(string fileName, ReadOnlyMemory<byte> data)>? files, int? autoArchiveMinutes, CancellationToken ct)
    {
        CreateForumPostRequest request = new()
        {
            name = title,
            auto_archive_duration = autoArchiveMinutes,
            applied_tags = payload.applied_tags,
            message = new ForumThreadMessage
            {
                content = payload.content,
                embeds = payload.embeds,
                components = payload.components,
                allowed_mentions = payload.allowed_mentions,
                attachments = payload.attachments,
                flags = payload.flags
            }
        };

        string route = $"/channels/{channelId}/threads";
        ForumPostResponse? response = files is { Count: > 0 }
            ? await _rest.PostMultipartAsync<ForumPostResponse>(route, request, files, ct).ConfigureAwait(false)
            : await _rest.PostAsync<ForumPostResponse>(route, request, ct).ConfigureAwait(false);
        return response?.message;
    }

    /// <summary>
    /// Gets a channel's archived threads.
    /// Example: foreach (var thread in await bot.GetArchivedThreadsAsync(channelId)) { }
    /// </summary>
    /// <param name="channelId">The parent channel</param>
    /// <param name="isPrivate">True for private threads, which requires the Manage Threads permission</param>
    public async Task<IEnumerable<DiscordChannel>> GetArchivedThreadsAsync(string channelId, bool isPrivate = false, CancellationToken ct = default)
    {
        string kind = isPrivate ? "private" : "public";
        ThreadListResponse? response = await _rest.GetAsync<ThreadListResponse>($"/channels/{channelId}/threads/archived/{kind}", ct).ConfigureAwait(false);
        return response?.threads ?? [];
    }

    /// <inheritdoc cref="GetArchivedThreadsAsync(string, bool, CancellationToken)"/>
    public Task<IEnumerable<DiscordChannel>> GetArchivedThreadsAsync(ulong channelId, bool isPrivate = false, CancellationToken ct = default)
        => GetArchivedThreadsAsync(ToId(channelId), isPrivate, ct);

    /// <summary>
    /// Gets a guild's active threads.
    /// Example: foreach (var thread in await bot.GetActiveThreadsAsync(guildId)) { }
    /// </summary>
    public async Task<IEnumerable<DiscordChannel>> GetActiveThreadsAsync(string guildId, CancellationToken ct = default)
    {
        ThreadListResponse? response = await _rest.GetAsync<ThreadListResponse>($"/guilds/{guildId}/threads/active", ct).ConfigureAwait(false);
        return response?.threads ?? [];
    }

    /// <inheritdoc cref="GetActiveThreadsAsync(string, CancellationToken)"/>
    public Task<IEnumerable<DiscordChannel>> GetActiveThreadsAsync(ulong guildId, CancellationToken ct = default)
        => GetActiveThreadsAsync(ToId(guildId), ct);

    // ---- Messages ----

    /// <summary>
    /// Unpins a message in a channel. Requires the PinMessages permission.
    /// </summary>
    public Task UnpinMessageAsync(ulong channelId, ulong messageId, CancellationToken ct = default)
        => _rest.UnpinMessageAsync(ToId(channelId), ToId(messageId), ct);

    /// <summary>
    /// Forwards a message to another channel. The bot must be able to read the message.
    /// Example: await bot.ForwardMessageAsync(logChannelId, channelId, messageId);
    /// </summary>
    public Task<DiscordMessage?> ForwardMessageAsync(string toChannelId, string fromChannelId, string messageId, CancellationToken ct = default)
    {
        CreateMessageRequest request = new()
        {
            message_reference = new MessageReference { type = ForwardReferenceType, channel_id = fromChannelId, message_id = messageId }
        };
        return _rest.PostAsync<DiscordMessage>($"/channels/{toChannelId}/messages", request, ct);
    }

    /// <inheritdoc cref="ForwardMessageAsync(string, string, string, CancellationToken)"/>
    public Task<DiscordMessage?> ForwardMessageAsync(ulong toChannelId, ulong fromChannelId, ulong messageId, CancellationToken ct = default)
        => ForwardMessageAsync(ToId(toChannelId), ToId(fromChannelId), ToId(messageId), ct);

    /// <summary>
    /// Searches a guild's messages. Needs the Read Message History permission and the Message Content intent.
    /// Discord may return fewer results than the limit.
    /// Example: var results = await bot.SearchMessagesAsync(guildId, "release notes");
    /// </summary>
    /// <param name="guildId">The guild to search</param>
    /// <param name="content">Text to look for</param>
    /// <param name="limit">Maximum results (1-25)</param>
    public async Task<IEnumerable<DiscordMessage>> SearchMessagesAsync(string guildId, string content, int limit = 25, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 25);

        string route = $"/guilds/{guildId}/messages/search?content={Uri.EscapeDataString(content)}&limit={limit}";
        for (int attempt = 0; ; attempt++)
        {
            MessageSearchResponse? response = await _rest.GetAsync<MessageSearchResponse>(route, ct).ConfigureAwait(false);

            // Discord answers without messages while it's still indexing the guild
            if (response?.messages is not null)
            {
                // Each result is an array that holds the matching message
                return response.messages.Where(static hit => hit.Length > 0).Select(static hit => hit[0]).ToArray();
            }

            if (response?.retry_after is null || attempt == SearchIndexRetries) return [];
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(response.retry_after.Value, 1)), ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc cref="SearchMessagesAsync(string, string, int, CancellationToken)"/>
    public Task<IEnumerable<DiscordMessage>> SearchMessagesAsync(ulong guildId, string content, int limit = 25, CancellationToken ct = default)
        => SearchMessagesAsync(ToId(guildId), content, limit, ct);

    // ---- Polls ----

    /// <summary>
    /// Ends a poll early and returns the updated message.
    /// Example: await bot.EndPollAsync(channelId, messageId);
    /// </summary>
    public Task<DiscordMessage?> EndPollAsync(string channelId, string messageId, CancellationToken ct = default)
        => _rest.PostAsync<DiscordMessage>($"/channels/{channelId}/polls/{messageId}/expire", EmptyPayload.Instance, ct);

    /// <inheritdoc cref="EndPollAsync(string, string, CancellationToken)"/>
    public Task<DiscordMessage?> EndPollAsync(ulong channelId, ulong messageId, CancellationToken ct = default)
        => EndPollAsync(ToId(channelId), ToId(messageId), ct);

    /// <summary>
    /// Gets everyone who voted for a poll answer.
    /// Example: var voters = await bot.GetPollVotersAsync(channelId, messageId, answerId);
    /// </summary>
    public async Task<IEnumerable<DiscordUser>> GetPollVotersAsync(string channelId, string messageId, int answerId, CancellationToken ct = default)
    {
        const int pageSize = 100;
        List<DiscordUser> all = [];
        ulong? after = null;
        while (true)
        {
            string route = $"/channels/{channelId}/polls/{messageId}/answers/{answerId}?limit={pageSize}" + (after is null ? string.Empty : $"&after={after}");
            PollVotersResponse? page = await _rest.GetAsync<PollVotersResponse>(route, ct).ConfigureAwait(false);
            if (page is null) return all;

            all.AddRange(page.users);
            if (page.users.Length < pageSize) return all;
            after = page.users[^1].Id;
        }
    }

    /// <inheritdoc cref="GetPollVotersAsync(string, string, int, CancellationToken)"/>
    public Task<IEnumerable<DiscordUser>> GetPollVotersAsync(ulong channelId, ulong messageId, int answerId, CancellationToken ct = default)
        => GetPollVotersAsync(ToId(channelId), ToId(messageId), answerId, ct);

    // ---- Members ----

    /// <summary>
    /// Finds members whose username or nickname starts with the query.
    /// Example: var members = await bot.SearchMembersAsync(guildId, "sam");
    /// </summary>
    /// <param name="guildId">The guild to search</param>
    /// <param name="query">The start of a username or nickname</param>
    /// <param name="limit">Maximum results (1-1000)</param>
    public async Task<IEnumerable<DiscordMember>> SearchMembersAsync(string guildId, string query, int limit = 100, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 1000);

        string route = $"/guilds/{guildId}/members/search?query={Uri.EscapeDataString(query)}&limit={limit}";
        DiscordMember[] members = await _rest.GetAsync<DiscordMember[]>(route, ct).ConfigureAwait(false) ?? [];
        if (members.Length == 0) return members;

        ulong id = ulong.Parse(guildId, CultureInfo.InvariantCulture);
        DiscordGuild guild = _cache.TryGetGuild(id, out DiscordGuild cached) ? cached : new DiscordGuild { Id = id, Name = string.Empty };
        foreach (DiscordMember member in members)
            member.Guild = guild;
        return members;
    }

    /// <inheritdoc cref="SearchMembersAsync(string, string, int, CancellationToken)"/>
    public Task<IEnumerable<DiscordMember>> SearchMembersAsync(ulong guildId, string query, int limit = 100, CancellationToken ct = default)
        => SearchMembersAsync(ToId(guildId), query, limit, ct);

    /// <summary>
    /// Bans up to 200 users at once. Requires the Ban Members and Manage Guild permissions.
    /// Example: var result = await bot.BulkBanAsync(guildId, userIds, reason: "Raid");
    /// </summary>
    /// <param name="guildId">The guild</param>
    /// <param name="userIds">Users to ban</param>
    /// <param name="deleteMessageSeconds">How many seconds of their recent messages to delete, up to 604800 (7 days)</param>
    /// <param name="ct">Cancellation token</param>
    /// <param name="reason">Reason shown in the audit log</param>
    public Task<BulkBanResult?> BulkBanAsync(string guildId, IEnumerable<ulong> userIds, int? deleteMessageSeconds = null, CancellationToken ct = default, string? reason = null)
    {
        string[] ids = userIds.Select(ToId).ToArray();
        if (ids.Length is 0 or > MaxBulkBanUsers)
            throw new ArgumentException($"Provide between 1 and {MaxBulkBanUsers} user IDs.", nameof(userIds));

        BulkBanRequest request = new() { user_ids = ids, delete_message_seconds = deleteMessageSeconds };
        return _rest.PostAsync<BulkBanResult>($"/guilds/{guildId}/bulk-ban", request, ct, reason);
    }

    /// <inheritdoc cref="BulkBanAsync(string, IEnumerable{ulong}, int?, CancellationToken, string?)"/>
    public Task<BulkBanResult?> BulkBanAsync(ulong guildId, IEnumerable<ulong> userIds, int? deleteMessageSeconds = null, CancellationToken ct = default, string? reason = null)
        => BulkBanAsync(ToId(guildId), userIds, deleteMessageSeconds, ct, reason);

    // ---- Application emojis ----

    private async Task<string> GetApplicationIdAsync(CancellationToken ct)
    {
        if (_applicationId is not null) return _applicationId;

        var application = await GetApplicationInfoAsync(ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Couldn't read the application's ID from Discord.");
        _applicationId = application.Id;
        return _applicationId;
    }

    /// <summary>
    /// Gets the emojis owned by your application, which work in every guild the bot is in.
    /// Example: var emojis = await bot.GetApplicationEmojisAsync();
    /// </summary>
    public async Task<IEnumerable<DiscordEmoji>> GetApplicationEmojisAsync(CancellationToken ct = default)
    {
        string applicationId = await GetApplicationIdAsync(ct).ConfigureAwait(false);
        ApplicationEmojiListResponse? response = await _rest.GetAsync<ApplicationEmojiListResponse>($"/applications/{applicationId}/emojis", ct).ConfigureAwait(false);
        return response?.items ?? [];
    }

    /// <summary>
    /// Creates an emoji owned by your application.
    /// Example: await bot.CreateApplicationEmojiAsync("party", "data:image/png;base64,...");
    /// </summary>
    /// <param name="name">Emoji name</param>
    /// <param name="imageBase64">The image as a base64 data URI</param>
    /// <param name="ct">Cancellation token</param>
    public async Task<DiscordEmoji?> CreateApplicationEmojiAsync(string name, string imageBase64, CancellationToken ct = default)
    {
        string applicationId = await GetApplicationIdAsync(ct).ConfigureAwait(false);
        CreateEmojiRequest request = new() { name = name, image = imageBase64 };
        return await _rest.PostAsync<DiscordEmoji>($"/applications/{applicationId}/emojis", request, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Renames an emoji owned by your application.
    /// Example: await bot.ModifyApplicationEmojiAsync(emojiId, "celebrate");
    /// </summary>
    public async Task<DiscordEmoji?> ModifyApplicationEmojiAsync(string emojiId, string name, CancellationToken ct = default)
    {
        string applicationId = await GetApplicationIdAsync(ct).ConfigureAwait(false);
        ModifyEmojiRequest request = new() { name = name };
        return await _rest.PatchAsync<DiscordEmoji>($"/applications/{applicationId}/emojis/{emojiId}", request, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes an emoji owned by your application.
    /// Example: await bot.DeleteApplicationEmojiAsync(emojiId);
    /// </summary>
    public async Task DeleteApplicationEmojiAsync(string emojiId, CancellationToken ct = default)
    {
        string applicationId = await GetApplicationIdAsync(ct).ConfigureAwait(false);
        await _rest.DeleteAsync($"/applications/{applicationId}/emojis/{emojiId}", ct).ConfigureAwait(false);
    }

    // ---- Thread, pin and gateway limit events ----

    private void WireThreadAndPinEvents(GatewayClient gateway)
    {
        gateway.ChannelPinsUpdate += OnChannelPinsUpdate;
        gateway.ThreadMemberUpdate += OnThreadMemberUpdate;
        gateway.ThreadMembersUpdate += OnThreadMembersUpdate;
        gateway.ThreadListSync += OnThreadListSync;
        gateway.GatewayRateLimited += OnGatewayRateLimited;
    }

    private void OnChannelPinsUpdate(object? sender, ChannelPinsUpdateEvent e)
        => DiscordEvents.RaiseChannelPinsUpdated(this, e);

    private void OnThreadMemberUpdate(object? sender, ThreadMemberUpdateEvent e)
        => DiscordEvents.RaiseThreadMemberUpdated(this, e);

    private void OnThreadMembersUpdate(object? sender, ThreadMembersUpdateEvent e)
        => DiscordEvents.RaiseThreadMembersUpdated(this, e);

    private void OnThreadListSync(object? sender, ThreadListSyncEvent e)
    {
        foreach (DiscordChannel thread in e.Threads)
            _cache.UpsertChannel(e.GuildId, thread);

        DiscordEvents.RaiseThreadListSynced(this, e);
    }

    /// <summary>
    /// A dropped member request would leave the guild's member list incomplete, so it's sent again
    /// after the wait Discord asks for.
    /// </summary>
    private void OnGatewayRateLimited(object? sender, GatewayRateLimitedEvent e)
    {
        _logger.Log(LogLevel.Warning, $"Gateway rate limited opcode {e.Opcode}. Retry after {e.RetryAfter.TotalSeconds:F1}s.");
        DiscordEvents.RaiseGatewayRateLimited(this, e);

        if (e.Opcode != GatewayClient.RequestGuildMembersOpcode || e.GuildId is not ulong guildId || sender is not GatewayClient gateway)
            return;

        TrackBackgroundTask(async () =>
        {
            await Task.Delay(e.RetryAfter, _cts.Token).ConfigureAwait(false);
            await gateway.RequestGuildMembersAsync(ToId(guildId), _cts.Token).ConfigureAwait(false);
        }, "member request retry");
    }
}
