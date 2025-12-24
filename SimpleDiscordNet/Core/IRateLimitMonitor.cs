using SimpleDiscordNet.Rest;

namespace SimpleDiscordNet.Core;

/// <summary>
/// Provides access to rate limit monitoring and statistics.
/// </summary>
public interface IRateLimitMonitor
{
    /// <summary>
    /// Get rate limit statistics for all buckets.
    /// </summary>
    IReadOnlyList<RateLimitBucketInfo> GetAllBuckets();

    /// <summary>
    /// Get rate limit statistics for a specific bucket by ID.
    /// </summary>
    RateLimitBucketInfo? GetBucket(string bucketId);

    /// <summary>
    /// Get buckets that are currently rate limited.
    /// </summary>
    IReadOnlyList<RateLimitBucketInfo> GetRateLimitedBuckets();

    /// <summary>
    /// Get the bucket with the highest utilization (lowest remaining percentage).
    /// </summary>
    RateLimitBucketInfo? GetMostUtilizedBucket();

    /// <summary>
    /// Get total number of 429 responses received across all buckets.
    /// </summary>
    long GetTotal429Count();

    /// <summary>
    /// Get total number of pre-emptive waits across all buckets.
    /// </summary>
    long GetTotalPreEmptiveWaits();
}
