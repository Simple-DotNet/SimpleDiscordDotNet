namespace SimpleDiscordNet.Models.Requests;

internal sealed class CreateMessageRequest
{
    public string? content { get; set; }
    public Embed[]? embeds { get; set; }
    public object[]? components { get; set; }
    public object[]? attachments { get; set; }
    public object? allowed_mentions { get; set; }
    public MessageReference? message_reference { get; set; }
    public string? thread_name { get; set; }
    public string[]? applied_tags { get; set; }
}

public sealed class MessageReference
{
    public string? message_id { get; set; }
    public string? channel_id { get; set; }
    public string? guild_id { get; set; }
    public bool? fail_if_not_exists { get; set; }
}
