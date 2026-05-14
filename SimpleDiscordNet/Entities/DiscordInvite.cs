namespace SimpleDiscordNet.Entities;

public sealed record DiscordInvite
{
    public required string Code { get; init; }
    public DiscordGuild? Guild { get; init; }
    public DiscordChannel? Channel { get; init; }
    public DiscordUser? Inviter { get; init; }
    public int? Target_Type { get; init; }
    public DiscordUser? Target_User { get; init; }
    public int? Approximate_Presence_Count { get; init; }
    public int? Approximate_Member_Count { get; init; }
    public DateTimeOffset? Expires_At { get; init; }
}
