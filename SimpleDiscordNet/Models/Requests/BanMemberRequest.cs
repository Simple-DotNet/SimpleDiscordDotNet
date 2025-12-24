namespace SimpleDiscordNet.Models.Requests;

/// <summary>
/// Request payload for banning a member.
/// </summary>
internal sealed class BanMemberRequest
{
    public int? delete_message_days { get; init; }
}
