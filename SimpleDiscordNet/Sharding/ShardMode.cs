namespace SimpleDiscordNet.Sharding;

/// <summary>
/// Sharding mode for the Discord bot.
/// Example: ShardMode.SingleProcess (all shards in one process) or ShardMode.Distributed (across multiple machines)
/// </summary>
public enum ShardMode
{
    /// <summary>
    /// All shards run in a single process with shared memory. No network coordination needed.
    /// Suitable for bots with up to ~10,000 guilds on a single powerful machine.
    /// Example: .WithSharding(ShardMode.SingleProcess)
    /// </summary>
    SingleProcess = 0,

    /// <summary>
    /// Shards distributed across multiple processes or machines with automatic coordination.
    /// Enables horizontal scaling, load balancing, and high availability.
    /// Requires network coordination between processes via HTTP.
    /// Example: .WithSharding(ShardMode.Distributed)
    /// </summary>
    Distributed = 1
}
