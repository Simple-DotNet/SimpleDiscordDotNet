using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Primitives;

namespace SimpleDiscordNet.Models.Requests;

internal sealed class StartThreadRequest
{
    public required string name { get; init; }
    public int? auto_archive_duration { get; init; }
    public int? type { get; init; }
}

internal sealed class CreateForumPostRequest
{
    public required string name { get; init; }
    public int? auto_archive_duration { get; init; }
    public string[]? applied_tags { get; init; }
    public required ForumThreadMessage message { get; init; }
}

internal sealed class ForumThreadMessage
{
    public string? content { get; init; }
    public Embed[]? embeds { get; init; }
    public IComponent[]? components { get; init; }
    public AllowedMentionsPayload? allowed_mentions { get; init; }
    public AttachmentReference[]? attachments { get; init; }
    public int? flags { get; init; }
}

internal sealed class ForumPostResponse
{
    public DiscordMessage? message { get; set; }
}

internal sealed class ThreadListResponse
{
    public DiscordChannel[] threads { get; set; } = [];
}

internal sealed class PollVotersResponse
{
    public DiscordUser[] users { get; set; } = [];
}

internal sealed class BulkBanRequest
{
    public required string[] user_ids { get; init; }
    public int? delete_message_seconds { get; init; }
}

internal sealed class MessageSearchResponse
{
    public DiscordMessage[][]? messages { get; set; }
    public double? retry_after { get; set; }
}

internal sealed class ApplicationEmojiListResponse
{
    public DiscordEmoji[] items { get; set; } = [];
}
