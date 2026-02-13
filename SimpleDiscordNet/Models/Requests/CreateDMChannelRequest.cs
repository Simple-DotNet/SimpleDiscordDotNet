namespace SimpleDiscordNet.Models.Requests;

internal sealed class CreateDMChannelRequest
{
    public required string recipient_id { get; set; }
}
