namespace SimpleDiscordNet.Models.Requests;

internal sealed class CreateMessageRequest
{
    public string? content { get; set; }
    public Embed[]? embeds { get; set; }
    public object[]? components { get; set; }
    public object[]? attachments { get; set; }
    public object? allowed_mentions { get; set; }
}
