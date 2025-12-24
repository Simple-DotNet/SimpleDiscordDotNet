namespace SimpleDiscordNet.Models;

// Minimal subset used for command registration and followups
public sealed record ApplicationInfo
{
    public required string Id { get; init; }
}
