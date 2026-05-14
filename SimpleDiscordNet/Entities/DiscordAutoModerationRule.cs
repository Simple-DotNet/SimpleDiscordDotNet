namespace SimpleDiscordNet.Entities;

public sealed record DiscordAutoModerationRule
{
    public required string Id { get; init; }
    public required string Guild_Id { get; init; }
    public required string Name { get; init; }
    public required string Creator_Id { get; init; }
    public int? Event_Type { get; init; }
    public int? Trigger_Type { get; init; }
    public bool? Enabled { get; init; }
    public string[]? Exempt_Roles { get; init; }
    public string[]? Exempt_Channels { get; init; }
}
