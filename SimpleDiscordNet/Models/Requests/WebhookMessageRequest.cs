using SimpleDiscordNet.Primitives;

namespace SimpleDiscordNet.Models.Requests;

internal sealed class WebhookMessageRequest
{
    public string? content { get; set; }
    public Embed[]? embeds { get; set; }
    public int? flags { get; set; }
    public IComponent[]? components { get; set; }
    public AttachmentReference[]? attachments { get; set; }
    public AllowedMentionsPayload? allowed_mentions { get; set; }
}
