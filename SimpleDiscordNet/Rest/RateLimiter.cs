using System.Collections.Concurrent;
using SimpleDiscordNet.Core;

namespace SimpleDiscordNet.Rest;

/// <summary>
/// Rate limiter with bucket management, global limiting, and request queuing.
/// Ensures no messages are lost while respecting Discord's rate limits.
/// </summary>
internal sealed class RateLimiter : IRateLimitMonitor, IDisposable
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
    public async Task<RateLimitHandle> AcquireAsync(HttpMethod method, string route, CancellationToken ct)
    {
        if (!RestRoute.IsGlobalLimitExempt(route))
        {
            await WaitForGlobalLimitAsync(route, ct).ConfigureAwait(false);
        }

        RateLimitBucket bucket = GetBucket(method, route, responseBucketId: null);
        IDisposable bucketLease = await bucket.AcquireAsync(ct).ConfigureAwait(false);

        return new RateLimitHandle(bucketLease);
    }

    /// <summary>
    /// Update rate limit information from a response.
    /// </summary>
    public Task UpdateFromResponseAsync(HttpMethod method, string route, HttpResponseMessage response)
        => GetBucket(method, route, GetBucketId(response)).UpdateFromHeadersAsync(response);

    /// <summary>
    /// Handle a 429 response by updating the appropriate bucket and retrying.
    /// A global 429 stops every route, so it also pauses the global limiter.
    /// </summary>
    public async Task Handle429Async(HttpMethod method, string route, HttpResponseMessage response, CancellationToken ct)
    {
        RateLimitBucket bucket = GetBucket(method, route, GetBucketId(response));
        await bucket.Handle429Async(response, ct).ConfigureAwait(false);

        if (RateLimitBucket.IsGlobalLimit(response))
        {
            DateTimeOffset resumeAt = _time.GetUtcNow() + RateLimitBucket.GetRetryAfter(response);
            lock (_globalLock)
            {
                _globalRemaining = 0;
                if (resumeAt > _globalResetAt)
                {
                    _globalResetAt = resumeAt;
                }
            }
        }
    }

    private static string? GetBucketId(HttpResponseMessage response)
        => response.Headers.TryGetValues("X-RateLimit-Bucket", out IEnumerable<string>? values) ? values.FirstOrDefault() : null;

    /// <summary>
    /// Finds the bucket for a request. Discord scopes a limit to the bucket hash plus the top-level
    /// resource, so the hash is learned per route template and combined with the resource here.
    /// </summary>
    private RateLimitBucket GetBucket(HttpMethod method, string route, string? responseBucketId)
    {
        string template = RestRoute.GetTemplate(route);
        string templateKey = $"{method.Method} {template}";

        if (responseBucketId is not null)
        {
            _routeToBucket[templateKey] = responseBucketId;
        }

        string bucketId = responseBucketId ?? (_routeToBucket.TryGetValue(templateKey, out string? mapped) ? mapped : templateKey);
        string bucketKey = $"{bucketId}:{RestRoute.GetMajorParameter(route)}";

        _lastAccess[bucketKey] = _time.GetUtcNow();
        return _buckets.GetOrAdd(bucketKey, static (key, arg) => new RateLimitBucket(key, arg.template, arg.time), (template, time: _time));
    }

    /// <inheritdoc />
    public IReadOnlyList<RateLimitBucketInfo> GetAllBuckets()
    {
        return _buckets.Values.Select(b => b.GetInfo()).ToList();
    }

    /// <inheritdoc />
    public RateLimitBucketInfo? GetBucket(string bucketId)
    {
        return _buckets.TryGetValue(bucketId, out var bucket) ? bucket.GetInfo() : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<RateLimitBucketInfo> GetRateLimitedBuckets()
        => GetAllBuckets().Where(static b => b.IsRateLimited).ToList();

    /// <inheritdoc />
    public RateLimitBucketInfo? GetMostUtilizedBucket()
        => GetAllBuckets().MinBy(static b => b.RemainingPercentage);

    /// <inheritdoc />
    public long GetTotal429Count() => GetAllBuckets().Sum(static b => b.Total429s);

    /// <inheritdoc />
    public long GetTotalPreEmptiveWaits() => GetAllBuckets().Sum(static b => b.TotalWaits);

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
