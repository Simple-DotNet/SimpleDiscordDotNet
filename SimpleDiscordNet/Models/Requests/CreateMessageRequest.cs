using SimpleDiscordNet.Primitives;

namespace SimpleDiscordNet.Models.Requests;

internal sealed class CreateMessageRequest
{
    public string? content { get; set; }
    public Embed[]? embeds { get; set; }
    public IComponent[]? components { get; set; }
    public AttachmentReference[]? attachments { get; set; }
    public AllowedMentionsPayload? allowed_mentions { get; set; }
    public MessageReference? message_reference { get; set; }
    public PollRequest? poll { get; set; }
    public int? flags { get; set; }
}

public sealed class MessageReference
{
    /// <summary>0 for a reply, 1 for a forward.</summary>
    public int? type { get; set; }
    public string? message_id { get; set; }
    public string? channel_id { get; set; }
    public string? guild_id { get; set; }
    public bool? fail_if_not_exists { get; set; }
}
