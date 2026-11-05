namespace SimpleDiscordNet.Events;

/// <summary>
/// Raised when Discord rejects a gateway request for being too frequent.
/// The library retries member requests by itself, so this is mainly for monitoring.
/// </summary>
public sealed record GatewayRateLimitedEvent
{
    /// <summary>The gateway opcode that was limited. 8 is a member request.</summary>
    public required int Opcode { get; init; }

    public required TimeSpan RetryAfter { get; init; }

    /// <summary>The guild, for member requests.</summary>
    public ulong? GuildId { get; init; }
}
