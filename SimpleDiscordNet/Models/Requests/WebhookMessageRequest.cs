namespace SimpleDiscordNet.Models.Requests;

internal sealed class WebhookMessageRequest
{
    public string? content { get; set; }
    public Embed[]? embeds { get; set; }
    public int? flags { get; set; }
    public object[]? components { get; set; }
}
