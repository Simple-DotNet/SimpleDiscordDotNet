namespace SimpleDiscordNet.Sharding;

/// <summary>
/// Provides information about a single shard connection.
/// Accessible via [ShardContext] attribute in commands or bot.Shards property.
/// Example:
/// <code>
/// [SlashCommand("info", "Get shard info")]
/// public async Task InfoAsync(InteractionContext ctx, [ShardContext] ShardInfo shard)
/// {
///     await ctx.RespondAsync($"Shard {shard.Id}/{shard.Total} | {shard.GuildCount} guilds | {shard.Latency}ms");
/// }
/// </code>
/// </summary>
public sealed class ShardInfo
{
    /// <summary>
    /// This shard's unique ID (0-indexed).
    /// Example: In a 4-shard setup, IDs are 0, 1, 2, 3.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Total number of shards across all processes.
    /// Example: 4 (shards 0-3)
    /// </summary>
    public int Total { get; init; }

    /// <summary>
    /// Current connection status of this shard.
    /// </summary>
    public ShardStatus Status { get; init; }

    /// <summary>
    /// Number of guilds (servers) this shard currently handles.
    /// </summary>
    public int GuildCount { get; init; }

    /// <summary>
    /// Discord gateway latency in milliseconds (round-trip time for heartbeat).
    /// Lower is better. Typical values: 30-100ms.
    /// </summary>
    public int Latency { get; init; }

    /// <summary>
    /// Read-only list of guild IDs assigned to this shard.
    /// </summary>
    public IReadOnlyList<string> GuildIds { get; init; } = [];

    /// <summary>
    /// Current events per second being processed by this shard.
    /// Includes message creates, updates, reactions, etc.
    /// </summary>
    public int EventsPerSecond { get; init; }

    /// <summary>
    /// Commands per second being processed by this shard.
    /// </summary>
    public int CommandsPerSecond { get; init; }

    /// <summary>
    /// URL of the process/machine hosting this shard (distributed mode only).
    /// Null in single-process mode.
    /// Example: "http://192.168.1.10:5000"
    /// </summary>
    public string? HostUrl { get; init; }
}
