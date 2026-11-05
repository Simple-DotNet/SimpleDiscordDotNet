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

/// <summary>
/// The outcome of a bulk ban.
/// </summary>
public sealed record BulkBanResult
{
    /// <summary>Users who were banned.</summary>
    public ulong[] Banned_Users { get; init; } = [];

    /// <summary>Users who couldn't be banned.</summary>
    public ulong[] Failed_Users { get; init; } = [];
}
