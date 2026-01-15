namespace SimpleDiscordNet.Entities;

/// <summary>
/// Represents a Discord guild ban.
/// Contains information about a banned user and the reason for the ban.
/// </summary>
public sealed record DiscordBan
{
    /// <summary>
    /// The reason for the ban (0-512 characters, or null if no reason provided).
    /// </summary>
    public string? reason { get; init; }

    /// <summary>
    /// The banned user.
    /// </summary>
    public required DiscordUser user { get; init; }
}
