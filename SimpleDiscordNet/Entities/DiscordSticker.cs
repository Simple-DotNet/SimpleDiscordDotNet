namespace SimpleDiscordNet.Entities;

public sealed record DiscordSticker
{
    public required string Id { get; init; }
    public string? Pack_Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? Tags { get; init; }
    public string? Asset { get; init; }
    public int? Type { get; init; }
    public int? Format_Type { get; init; }
    public bool? Available { get; init; }
    public string? Guild_Id { get; init; }
    public DiscordUser? User { get; init; }
    public int? Sort_Value { get; init; }
}
