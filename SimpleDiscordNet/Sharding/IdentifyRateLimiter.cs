namespace SimpleDiscordNet.Sharding;

/// <summary>
/// Paces gateway identifies. Discord allows max_concurrency identifies every 5 seconds, grouped by
/// shard id modulo max_concurrency. This covers the shards in one process, so workers starting at
/// the same moment can still collide. Discord answers that with an invalid session, which is retried.
/// </summary>
internal sealed class IdentifyRateLimiter
{
    private static readonly TimeSpan IdentifyInterval = TimeSpan.FromSeconds(5);

    private readonly TimeProvider _time;
    private readonly SemaphoreSlim[] _gates;
    private readonly DateTimeOffset[] _nextIdentify;

    public IdentifyRateLimiter(int maxConcurrency, TimeProvider? time = null)
    {
        int buckets = Math.Max(1, maxConcurrency);
        _time = time ?? TimeProvider.System;
        _gates = new SemaphoreSlim[buckets];
        _nextIdentify = new DateTimeOffset[buckets];
        for (int i = 0; i < buckets; i++)
        {
            _gates[i] = new SemaphoreSlim(1, 1);
        }
    }

    /// <summary>
    /// Waits until this shard may identify.
    /// </summary>
    public async Task WaitAsync(int shardId, CancellationToken ct)
    {
        int bucket = shardId % _gates.Length;
        await _gates[bucket].WaitAsync(ct).ConfigureAwait(false);
        try
        {
            TimeSpan wait = _nextIdentify[bucket] - _time.GetUtcNow();
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, _time, ct).ConfigureAwait(false);
            }
            _nextIdentify[bucket] = _time.GetUtcNow() + IdentifyInterval;
        }
        finally
        {
            _gates[bucket].Release();
        }
    }
}
