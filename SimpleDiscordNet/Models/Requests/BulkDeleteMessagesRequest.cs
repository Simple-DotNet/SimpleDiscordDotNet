namespace SimpleDiscordNet.Models.Requests;

/// <summary>
/// Request payload for bulk deleting messages.
/// </summary>
internal sealed class BulkDeleteMessagesRequest
{
    public required string[] messages { get; init; }
}
