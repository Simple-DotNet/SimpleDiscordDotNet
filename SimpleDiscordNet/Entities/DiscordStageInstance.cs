namespace SimpleDiscordNet.Entities;

public sealed record DiscordStageInstance
{
    public required string Id { get; init; }
    public required string Guild_Id { get; init; }
    public required string Channel_Id { get; init; }
    public required string Topic { get; init; }
    public int? Privacy_Level { get; init; }
    public bool? Discoverable_Disabled { get; init; }
    public string? Guild_Scheduled_Event_Id { get; init; }
}
