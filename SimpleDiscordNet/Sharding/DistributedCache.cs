using System.Collections.Concurrent;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Logging;
using SimpleDiscordNet.Sharding.Models;

namespace SimpleDiscordNet.Sharding;

/// <summary>
/// Provides cross-shard cache queries in distributed mode using shard calculation.
/// Routes queries to the correct worker based on guild ID via ShardCalculator.
/// Example: var guild = await cache.GetGuildAsync("123456789", peers, logger);
/// </summary>
internal sealed class DistributedCache
{
    private readonly ShardHttpClient _client;
    private readonly int _totalShards;
    private readonly NativeLogger _logger;

    public DistributedCache(ShardHttpClient client, int totalShards, NativeLogger logger)
    {
        _client = client;
        _totalShards = totalShards;
        _logger = logger;
    }

    /// <summary>
    /// Gets a guild by ID, querying the appropriate worker based on shard calculation.
    /// Example: var guild = await cache.GetGuildAsync("123456789", peers);
    /// </summary>
    public async Task<DiscordGuild?> GetGuildAsync(string guildId, ConcurrentDictionary<string, PeerNode> peers, CancellationToken ct = default)
    {
        int shardId = ShardCalculator.CalculateShardId(guildId.AsSpan(), _totalShards);
        PeerNode? worker = FindWorkerForShard(shardId, peers);

        if (worker == null)
        {
            _logger.Log(LogLevel.Warning, $"No worker found for shard {shardId} (guild {guildId})");
            return null;
        }

        try
        {
            PeerNodeState state = worker.ToState();
            // Query worker's cache endpoint
            return await _client.GetAsync<DiscordGuild>($"{state.Url}/cache/guild/{guildId}", ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            PeerNodeState state = worker.ToState();
            _logger.Log(LogLevel.Error, $"Failed to query guild {guildId} from worker {state.ProcessId}: {ex.Message}", ex);
            return null;
        }
    }

    /// <summary>
    /// Gets a channel by ID, querying the appropriate worker based on guild ID.
    /// Requires guild ID to calculate shard.
    /// Example: var channel = await cache.GetChannelAsync("channel123", "guild456", peers);
    /// </summary>
    public async Task<DiscordChannel?> GetChannelAsync(string channelId, string guildId, ConcurrentDictionary<string, PeerNode> peers, CancellationToken ct = default)
    {
        int shardId = ShardCalculator.CalculateShardId(guildId.AsSpan(), _totalShards);
        PeerNode? worker = FindWorkerForShard(shardId, peers);

        if (worker == null)
        {
            _logger.Log(LogLevel.Warning, $"No worker found for shard {shardId} (guild {guildId})");
            return null;
        }

        try
        {
            PeerNodeState state = worker.ToState();
            return await _client.GetAsync<DiscordChannel>($"{state.Url}/cache/channel/{channelId}", ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            PeerNodeState state = worker.ToState();
            _logger.Log(LogLevel.Error, $"Failed to query channel {channelId} from worker {state.ProcessId}: {ex.Message}", ex);
            return null;
        }
    }

    /// <summary>
    /// Gets a member by user and guild ID, querying the appropriate worker.
    /// Example: var member = await cache.GetMemberAsync("user123", "guild456", peers);
    /// </summary>
    public async Task<DiscordMember?> GetMemberAsync(string userId, string guildId, ConcurrentDictionary<string, PeerNode> peers, CancellationToken ct = default)
    {
        int shardId = ShardCalculator.CalculateShardId(guildId.AsSpan(), _totalShards);
        PeerNode? worker = FindWorkerForShard(shardId, peers);

        if (worker == null)
        {
            _logger.Log(LogLevel.Warning, $"No worker found for shard {shardId} (guild {guildId})");
            return null;
        }

        try
        {
            PeerNodeState state = worker.ToState();
            return await _client.GetAsync<DiscordMember>($"{state.Url}/cache/member/{guildId}/{userId}", ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            PeerNodeState state = worker.ToState();
            _logger.Log(LogLevel.Error, $"Failed to query member {userId} from worker {state.ProcessId}: {ex.Message}", ex);
            return null;
        }
    }

    private static PeerNode? FindWorkerForShard(int shardId, ConcurrentDictionary<string, PeerNode> peers)
    {
        return peers.Values.FirstOrDefault(p => p.AssignedShards.Contains(shardId));
    }

    /// <summary>
    /// Gets the shard ID for a given guild ID.
    /// Example: int shardId = cache.GetShardIdForGuild("123456789");
    /// </summary>
    public int GetShardIdForGuild(string guildId)
    {
        return ShardCalculator.CalculateShardId(guildId.AsSpan(), _totalShards);
    }

    /// <summary>
    /// Gets the worker responsible for a given guild ID.
    /// Example: var worker = cache.GetWorkerForGuild("123456789", peers);
    /// </summary>
    public PeerNode? GetWorkerForGuild(string guildId, ConcurrentDictionary<string, PeerNode> peers)
    {
        int shardId = GetShardIdForGuild(guildId);
        return FindWorkerForShard(shardId, peers);
    }
}
