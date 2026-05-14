namespace SimpleDiscordNet.Entities;

public sealed record DiscordScheduledEvent
{
    public required string Id { get; init; }
    public required string Guild_Id { get; init; }
    public string? Channel_Id { get; init; }
    public string? Creator_Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public DateTimeOffset? Scheduled_Start_Time { get; init; }
    public DateTimeOffset? Scheduled_End_Time { get; init; }
    public int? Privacy_Level { get; init; }
    public int? Status { get; init; }
    public int? Entity_Type { get; init; }
    public string? Entity_Id { get; init; }
    public DiscordUser? Creator { get; init; }
    public int? User_Count { get; init; }
    public string? Image { get; init; }
}
