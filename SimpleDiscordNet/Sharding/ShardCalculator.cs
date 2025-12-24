using System;

namespace SimpleDiscordNet.Sharding;

/// <summary>
/// Utility for calculating which shard a guild belongs to using Discord's formula.
/// Uses span-based operations for zero-allocation calculations.
/// </summary>
internal static class ShardCalculator
{
    /// <summary>
    /// Calculates which shard a guild belongs to using Discord's formula: (guild_id >> 22) % num_shards.
    /// Uses ReadOnlySpan for zero-allocation parsing.
    /// Example: CalculateShardId("123456789012345678", 4) → 2
    /// </summary>
    /// <param name="guildId">Guild ID as a span of characters</param>
    /// <param name="totalShards">Total number of shards</param>
    /// <returns>Shard ID (0-indexed)</returns>
    public static int CalculateShardId(ReadOnlySpan<char> guildId, int totalShards)
    {
        if (totalShards <= 0)
            throw new ArgumentOutOfRangeException(nameof(totalShards), "Total shards must be greater than 0");

        if (!ulong.TryParse(guildId, out ulong id))
            throw new ArgumentException($"Invalid guild ID: {guildId.ToString()}", nameof(guildId));

        return (int)((id >> 22) % (ulong)totalShards);
    }

    /// <summary>
    /// Calculates which shard a guild belongs to (string overload).
    /// Example: CalculateShardId("123456789012345678", 4) → 2
    /// </summary>
    public static int CalculateShardId(string guildId, int totalShards)
        => CalculateShardId(guildId.AsSpan(), totalShards);
}
