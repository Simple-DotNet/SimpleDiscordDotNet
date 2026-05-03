using System.Globalization;

namespace SimpleDiscordNet.Rest;

/// <summary>
/// Represents a Discord rate limit bucket with request tracking and queuing.
/// </summary>
internal sealed class RateLimitBucket
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly TimeProvider _time;

    public string BucketId { get; }
    public string Route { get; }

    private int _limit = 1;
    private int _remaining = 1;
    private DateTimeOffset _resetAt = DateTimeOffset.MinValue;
    private bool _isGlobal;

    private long _totalRequests;
    private long _totalWaits;
    private long _total429s;

    public RateLimitBucket(string bucketId, string route, TimeProvider time)
    {
        BucketId = bucketId;
        Route = route;
        _time = time;
    }

    public async Task<IDisposable> AcquireAsync(CancellationToken ct)
    {
        while (true)
        {
            await _semaphore.WaitAsync(ct).ConfigureAwait(false);
            TimeSpan? waitDelay = null;
            try
            {
                DateTimeOffset now = _time.GetUtcNow();

                if (now >= _resetAt)
                {
                    _remaining = _limit > 0 ? _limit : 1;
                }

                if (_remaining <= 0 && _resetAt > now)
                {
                    waitDelay = _resetAt - now;
                    _totalWaits++;

                    RateLimitEventManager.RaisePreEmptiveWait(new RateLimitPreEmptiveWaitEvent
                    {
                        BucketId = BucketId,
                        Route = Route,
                        Remaining = _remaining,
                        Limit = _limit,
                        ResetAt = _resetAt,
                        WaitDuration = waitDelay.Value,
                        IsGlobal = _isGlobal,
                        Timestamp = now
                    });
                }
                else if (_remaining > 0)
                {
                    _remaining--;
                    _totalRequests++;
                    return NoOpDisposable.Instance;
                }
            }
            finally
            {
                _semaphore.Release();
            }

            await Task.Delay(waitDelay ?? TimeSpan.FromMilliseconds(200), ct).ConfigureAwait(false);
        }
    }

    public async Task UpdateFromHeadersAsync(HttpResponseMessage response)
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            DateTimeOffset now = _time.GetUtcNow();
            bool wasUpdated = false;

            if (response.Headers.TryGetValues("X-RateLimit-Limit", out var limitValues))
            {
                using var enumerator = limitValues.GetEnumerator();
                if (enumerator.MoveNext() && int.TryParse(enumerator.Current.AsSpan(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int limit))
                {
                    _limit = limit;
                    wasUpdated = true;
                }
            }

            if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remainingValues))
            {
                using var enumerator = remainingValues.GetEnumerator();
                if (enumerator.MoveNext() && int.TryParse(enumerator.Current.AsSpan(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int remaining))
                {
                    _remaining = remaining;
                    wasUpdated = true;
                }
            }

            if (response.Headers.TryGetValues("X-RateLimit-Reset", out var resetValues))
            {
                using var enumerator = resetValues.GetEnumerator();
                if (enumerator.MoveNext() && double.TryParse(enumerator.Current.AsSpan(), NumberStyles.Float, CultureInfo.InvariantCulture, out double resetTimestamp))
                {
                    _resetAt = DateTimeOffset.FromUnixTimeSeconds((long)resetTimestamp);
                    wasUpdated = true;
                }
            }

            _isGlobal = false;
            if (response.Headers.TryGetValues("X-RateLimit-Global", out var globalValues))
            {
                using var enumerator = globalValues.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    _isGlobal = enumerator.Current.AsSpan().Equals("true", StringComparison.Ordinal);
                }
            }

            if (wasUpdated)
            {
                RateLimitEventManager.RaiseBucketUpdated(new RateLimitBucketUpdateEvent
                {
                    BucketId = BucketId,
                    Route = Route,
                    Limit = _limit,
                    Remaining = _remaining,
                    ResetAt = _resetAt,
                    IsGlobal = _isGlobal,
                    Timestamp = now
                });
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task Handle429Async(HttpResponseMessage response, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _total429s++;
            DateTimeOffset now = _time.GetUtcNow();

            TimeSpan retryAfter = TimeSpan.FromSeconds(1);
            if (response.Headers.TryGetValues("Retry-After", out IEnumerable<string>? retryValues))
            {
                using var enumerator = retryValues.GetEnumerator();
                if (enumerator.MoveNext() && double.TryParse(enumerator.Current.AsSpan(), NumberStyles.Float, CultureInfo.InvariantCulture, out double retrySeconds))
                {
                    retryAfter = TimeSpan.FromSeconds(retrySeconds);
                }
            }

            bool isGlobal = false;
            if (response.Headers.TryGetValues("X-RateLimit-Global", out IEnumerable<string>? globalValues))
            {
                using IEnumerator<string> enumerator = globalValues.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    isGlobal = enumerator.Current.AsSpan().Equals("true", StringComparison.Ordinal);
                }
            }

            _resetAt = now + retryAfter > _resetAt ? now + retryAfter : _resetAt;
            _remaining = 0;
            _isGlobal = isGlobal;

            RateLimitEventManager.RaiseHit(new RateLimitHitEvent
            {
                BucketId = BucketId,
                Route = Route,
                Limit = _limit,
                ResetAt = _resetAt,
                RetryAfter = retryAfter,
                IsGlobal = isGlobal,
                Total429Count = _total429s,
                Timestamp = now
            });
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public RateLimitBucketInfo GetInfo()
    {
        return new RateLimitBucketInfo
        {
            BucketId = BucketId,
            Route = Route,
            Limit = _limit,
            Remaining = _remaining,
            ResetAt = _resetAt,
            IsGlobal = _isGlobal,
            TotalRequests = _totalRequests,
            TotalWaits = _totalWaits,
            Total429s = _total429s,
            Timestamp = _time.GetUtcNow()
        };
    }

    private sealed class NoOpDisposable : IDisposable
    {
        public static readonly NoOpDisposable Instance = new();
        public void Dispose() { }
    }
}
