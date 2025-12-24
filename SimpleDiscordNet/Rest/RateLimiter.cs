using System.Collections.Concurrent;

namespace SimpleDiscordNet.Rest;

/// <summary>
/// Rate limiter with bucket management, global limiting, and request queuing.
/// Ensures no messages are lost while respecting Discord's rate limits.
/// </summary>
internal sealed class RateLimiter : IDisposable
{
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<string, RateLimitBucket> _buckets = new();
    private readonly SemaphoreSlim _globalLimiter;
    private readonly Timer _globalResetTimer;

    // Global rate limit: 50 requests per second
    private const int GlobalLimit = 50;
    private int _globalRemaining = GlobalLimit;
    private DateTimeOffset _globalResetAt;
    private readonly object _globalLock = new();

    public RateLimiter(TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _globalLimiter = new SemaphoreSlim(1, 1);
        _globalResetAt = _time.GetUtcNow().AddSeconds(1);

        // Reset global counter every second
        _globalResetTimer = new Timer(_ => ResetGlobalLimit(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Acquire a slot for making a request to the specified route.
    /// This will queue the request if necessary and return when it's safe to proceed.
    /// </summary>
    public async Task<RateLimitHandle> AcquireAsync(string route, CancellationToken ct)
    {
        // Wait for global rate limit
        await WaitForGlobalLimitAsync(route, ct).ConfigureAwait(false);

        // Get or create bucket for this route (will be updated with actual bucket ID from headers)
        string bucketKey = GetBucketKey(route);
        RateLimitBucket bucket = _buckets.GetOrAdd(bucketKey, static (key, arg) => new RateLimitBucket(key, arg.route, arg.time), (route, time: _time));

        // Acquire slot in the bucket
        IDisposable bucketLease = await bucket.AcquireAsync(ct).ConfigureAwait(false);

        return new RateLimitHandle(bucket, bucketLease, this);
    }

    /// <summary>
    /// Update rate limit information from a response.
    /// </summary>
    public async Task UpdateFromResponseAsync(string route, HttpResponseMessage response)
    {
        // Update bucket ID if provided in headers
        string? bucketId = null;
        if (response.Headers.TryGetValues("X-RateLimit-Bucket", out var bucketValues))
        {
            bucketId = bucketValues.FirstOrDefault();
        }

        // Get the appropriate bucket
        string bucketKey = bucketId ?? GetBucketKey(route);
        RateLimitBucket bucket = _buckets.GetOrAdd(bucketKey, static (key, arg) => new RateLimitBucket(key, arg.route, arg.time), (route, time: _time));

        // If we got a bucket ID from Discord, migrate the route to use that bucket
        if (bucketId != null && bucketId != GetBucketKey(route))
        {
            _buckets.TryAdd(bucketId, bucket);
        }

        // Update bucket state from headers
        await bucket.UpdateFromHeadersAsync(response).ConfigureAwait(false);
    }

    /// <summary>
    /// Handle a 429 response by updating the appropriate bucket and retrying.
    /// </summary>
    public async Task Handle429Async(string route, HttpResponseMessage response, CancellationToken ct)
    {
        string bucketKey = GetBucketKey(route);
        if (response.Headers.TryGetValues("X-RateLimit-Bucket", out IEnumerable<string>? bucketValues))
        {
            string? bucketId = bucketValues.FirstOrDefault();
            if (bucketId != null)
            {
                bucketKey = bucketId;
            }
        }

        RateLimitBucket bucket = _buckets.GetOrAdd(bucketKey, static (key, arg) => new RateLimitBucket(key, arg.route, arg.time), (route, time: _time));
        await bucket.Handle429Async(response, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Get current statistics for all rate limit buckets.
    /// </summary>
    public IReadOnlyList<RateLimitBucketInfo> GetAllBucketInfo()
    {
        return _buckets.Values.Select(b => b.GetInfo()).ToList();
    }

    /// <summary>
    /// Get statistics for a specific bucket.
    /// </summary>
    public RateLimitBucketInfo? GetBucketInfo(string bucketId)
    {
        return _buckets.TryGetValue(bucketId, out var bucket) ? bucket.GetInfo() : null;
    }

    private async Task WaitForGlobalLimitAsync(string route, CancellationToken ct)
    {
        await _globalLimiter.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            lock (_globalLock)
            {
                DateTimeOffset now = _time.GetUtcNow();

                // If we've exceeded the global limit, wait until reset
                if (_globalRemaining <= 0 && _globalResetAt > now)
                {
                    TimeSpan waitTime = _globalResetAt - now;

                    RateLimitEventManager.RaisePreEmptiveWait(new RateLimitPreEmptiveWaitEvent
                    {
                        BucketId = "global",
                        Route = route,
                        Remaining = _globalRemaining,
                        Limit = GlobalLimit,
                        ResetAt = _globalResetAt,
                        WaitDuration = waitTime,
                        IsGlobal = true,
                        Timestamp = now
                    });

                    // Release the semaphore and wait
                    _globalLimiter.Release();
                    Task.Delay(waitTime, ct).ConfigureAwait(false).GetAwaiter().GetResult();
                    _globalLimiter.WaitAsync(ct).ConfigureAwait(false).GetAwaiter().GetResult();

                    // After waiting, reset should have occurred
                    _globalRemaining = GlobalLimit;
                    _globalResetAt = now.AddSeconds(1);
                }

                // Decrement global counter
                if (_globalRemaining > 0)
                {
                    _globalRemaining--;
                }
            }
        }
        finally
        {
            _globalLimiter.Release();
        }
    }

    private void ResetGlobalLimit()
    {
        lock (_globalLock)
        {
            DateTimeOffset now = _time.GetUtcNow();
            if (now < _globalResetAt) return;
            _globalRemaining = GlobalLimit;
            _globalResetAt = now.AddSeconds(1);
        }
    }

    private static string GetBucketKey(string route)
    {
        // Extract major parameters from route for bucketing
        // Discord groups routes by major parameters (guild_id, channel_id, webhook_id)
        // For now, use the route itself as the key until we get the bucket ID from headers
        return route;
    }

    private sealed class PendingRequest
    {
        public required string Route { get; init; }
        public required TaskCompletionSource<bool> CompletionSource { get; init; }
        public required CancellationToken CancellationToken { get; init; }
    }

    public void Dispose()
    {
        _globalResetTimer.Dispose();
        _globalLimiter.Dispose();
    }
}

/// <summary>
/// Handle returned from acquiring a rate limit slot.
/// Provides access to the bucket for updating after the request completes.
/// </summary>
public sealed class RateLimitHandle : IDisposable
{
    private readonly RateLimitBucket _bucket;
    private readonly IDisposable _bucketLease;
    private readonly RateLimiter _limiter;

    internal RateLimitHandle(RateLimitBucket bucket, IDisposable bucketLease, RateLimiter limiter)
    {
        _bucket = bucket;
        _bucketLease = bucketLease;
        _limiter = limiter;
    }

    internal RateLimitBucket Bucket => _bucket;

    public void Dispose()
    {
        _bucketLease.Dispose();
    }
}
