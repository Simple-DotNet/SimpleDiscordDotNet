namespace SimpleDiscordNet.Entities;

public sealed record DiscordWebhook
{
    public required string Id { get; init; }
    public int? Type { get; init; }
    public string? Guild_Id { get; init; }
    public required string Channel_Id { get; init; }
    public DiscordUser? User { get; init; }
    public string? Name { get; init; }
    public string? Avatar { get; init; }
    public string? Token { get; init; }
    public string? Application_Id { get; init; }
    public string? Url { get; init; }
}
