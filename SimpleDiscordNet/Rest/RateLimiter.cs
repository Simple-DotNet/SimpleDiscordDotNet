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
    private readonly ConcurrentDictionary<string, string> _routeToBucket = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastAccess = new();
    private readonly SemaphoreSlim _globalLimiter;
    private readonly Timer _globalResetTimer;
    private readonly Timer _cleanupTimer;

    private const int GlobalLimit = 50;
    private int _globalRemaining = GlobalLimit;
    private DateTimeOffset _globalResetAt;
    private readonly object _globalLock = new();

    private static readonly TimeSpan BucketEvictionAge = TimeSpan.FromMinutes(10);

    public RateLimiter(TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _globalLimiter = new SemaphoreSlim(1, 1);
        _globalResetAt = _time.GetUtcNow().AddSeconds(1);

        _globalResetTimer = new Timer(_ => ResetGlobalLimit(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        _cleanupTimer = new Timer(_ => CleanupUnusedBuckets(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Acquire a slot for making a request to the specified route.
    /// This will queue the request if necessary and return when it's safe to proceed.
    /// </summary>
    public async Task<RateLimitHandle> AcquireAsync(string route, CancellationToken ct)
    {
        await WaitForGlobalLimitAsync(route, ct).ConfigureAwait(false);

        string bucketKey = _routeToBucket.TryGetValue(route, out string? mappedBucketId)
            ? mappedBucketId
            : GetBucketKey(route);

        _lastAccess[bucketKey] = _time.GetUtcNow();
        RateLimitBucket bucket = _buckets.GetOrAdd(bucketKey, static (key, arg) => new RateLimitBucket(key, arg.route, arg.time), (route, time: _time));

        IDisposable bucketLease = await bucket.AcquireAsync(ct).ConfigureAwait(false);

        return new RateLimitHandle(bucketLease);
    }

    /// <summary>
    /// Update rate limit information from a response.
    /// </summary>
    public async Task UpdateFromResponseAsync(string route, HttpResponseMessage response)
    {
        string? bucketId = null;
        if (response.Headers.TryGetValues("X-RateLimit-Bucket", out var bucketValues))
        {
            bucketId = bucketValues.FirstOrDefault();
        }

        string bucketKey = bucketId
            ?? (_routeToBucket.TryGetValue(route, out string? existingMapping) ? existingMapping : GetBucketKey(route));

        _lastAccess[bucketKey] = _time.GetUtcNow();
        RateLimitBucket bucket = _buckets.GetOrAdd(bucketKey, static (key, arg) => new RateLimitBucket(key, arg.route, arg.time), (route, time: _time));

        if (bucketId != null)
        {
            _routeToBucket[route] = bucketId;
        }

        await bucket.UpdateFromHeadersAsync(response).ConfigureAwait(false);
    }

    /// <summary>
    /// Handle a 429 response by updating the appropriate bucket and retrying.
    /// </summary>
    public async Task Handle429Async(string route, HttpResponseMessage response, CancellationToken ct)
    {
        string bucketKey;
        if (response.Headers.TryGetValues("X-RateLimit-Bucket", out IEnumerable<string>? bucketValues))
        {
            string? responseBucketId = bucketValues.FirstOrDefault();
            if (responseBucketId != null)
            {
                bucketKey = responseBucketId;
                _routeToBucket[route] = responseBucketId;
            }
            else if (_routeToBucket.TryGetValue(route, out string? mapped))
            {
                bucketKey = mapped;
            }
            else
            {
                bucketKey = GetBucketKey(route);
            }
        }
        else if (_routeToBucket.TryGetValue(route, out string? mapped))
        {
            bucketKey = mapped;
        }
        else
        {
            bucketKey = GetBucketKey(route);
        }

        _lastAccess[bucketKey] = _time.GetUtcNow();
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
        while (true)
        {
            await _globalLimiter.WaitAsync(ct).ConfigureAwait(false);
            TimeSpan waitTime = TimeSpan.Zero;
            bool shouldWait = false;
            try
            {
                lock (_globalLock)
                {
                    DateTimeOffset now = _time.GetUtcNow();
                    if (now >= _globalResetAt)
                    {
                        _globalRemaining = GlobalLimit;
                        _globalResetAt = now.AddSeconds(1);
                    }

                    if (_globalRemaining <= 0 && _globalResetAt > now)
                    {
                        waitTime = _globalResetAt - now;
                        shouldWait = true;

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
                    }
                    else if (_globalRemaining > 0)
                    {
                        _globalRemaining--;
                        return;
                    }
                }
            }
            finally
            {
                _globalLimiter.Release();
            }

            if (shouldWait)
            {
                await Task.Delay(waitTime, ct).ConfigureAwait(false);
            }
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
        return route;
    }

    private void CleanupUnusedBuckets()
    {
        DateTimeOffset cutoff = _time.GetUtcNow() - BucketEvictionAge;

        var candidates = _lastAccess.Where(kvp => kvp.Value < cutoff).Select(kvp => kvp.Key).ToList();

        foreach (string bucketKey in candidates)
        {
            if (_lastAccess.TryGetValue(bucketKey, out DateTimeOffset ts) && ts < cutoff)
            {
                _buckets.TryRemove(bucketKey, out _);
                _lastAccess.TryRemove(bucketKey, out _);
            }
        }

        foreach (var kvp in _routeToBucket)
        {
            if (!_buckets.ContainsKey(kvp.Value))
            {
                _routeToBucket.TryRemove(kvp.Key, out _);
            }
        }
    }

    public void Dispose()
    {
        _cleanupTimer.Dispose();
        _globalResetTimer.Dispose();
        _globalLimiter.Dispose();
    }
}

/// <summary>
/// Handle returned from acquiring a rate limit slot.
/// Disposing releases the acquired slot back to the bucket.
/// </summary>
internal sealed class RateLimitHandle : IDisposable
{
    private readonly IDisposable _bucketLease;

    internal RateLimitHandle(IDisposable bucketLease)
    {
        _bucketLease = bucketLease;
    }

    public void Dispose()
    {
        _bucketLease.Dispose();
    }
}
