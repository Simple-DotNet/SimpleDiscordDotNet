namespace SimpleDiscordNet.Rest;

/// <summary>
/// Event raised when a rate limit bucket is updated with new information from Discord.
/// </summary>
public sealed class RateLimitBucketUpdateEvent
{
    public required string BucketId { get; init; }
    public required string Route { get; init; }
    public required int Limit { get; init; }
    public required int Remaining { get; init; }
    public required DateTimeOffset ResetAt { get; init; }
    public required bool IsGlobal { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Event raised when a rate limit is hit (429 response received).
/// </summary>
public sealed class RateLimitHitEvent
{
    public required string BucketId { get; init; }
    public required string Route { get; init; }
    public required int Limit { get; init; }
    public required DateTimeOffset ResetAt { get; init; }
    public required TimeSpan RetryAfter { get; init; }
    public required bool IsGlobal { get; init; }
    public required long Total429Count { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Event raised when the rate limiter proactively waits before making a request.
/// This prevents 429 responses by respecting the rate limit window.
/// </summary>
public sealed class RateLimitPreEmptiveWaitEvent
{
    public required string BucketId { get; init; }
    public required string Route { get; init; }
    public required int Remaining { get; init; }
    public required int Limit { get; init; }
    public required DateTimeOffset ResetAt { get; init; }
    public required TimeSpan WaitDuration { get; init; }
    public required bool IsGlobal { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Event raised when a request is enqueued due to global rate limiting.
/// </summary>
public sealed class RateLimitRequestQueuedEvent
{
    public required string Route { get; init; }
    public required int QueuePosition { get; init; }
    public required int QueueLength { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Snapshot of a rate limit bucket's current state.
/// </summary>
public sealed class RateLimitBucketInfo
{
    public required string BucketId { get; init; }
    public required string Route { get; init; }
    public required int Limit { get; init; }
    public required int Remaining { get; init; }
    public required DateTimeOffset ResetAt { get; init; }
    public required bool IsGlobal { get; init; }
    public required long TotalRequests { get; init; }
    public required long TotalWaits { get; init; }
    public required long Total429s { get; init; }
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Calculate the percentage of rate limit remaining.
    /// </summary>
    public double RemainingPercentage => Limit > 0 ? (double)Remaining / Limit * 100 : 0;

    /// <summary>
    /// Time until the rate limit resets.
    /// </summary>
    public TimeSpan TimeUntilReset => ResetAt > Timestamp ? ResetAt - Timestamp : TimeSpan.Zero;

    /// <summary>
    /// Whether this bucket is currently rate limited.
    /// </summary>
    public bool IsRateLimited => Remaining <= 0 && ResetAt > Timestamp;
}

/// <summary>
/// Global event manager for rate limit events.
/// Provides events for monitoring and responding to rate limit activity.
/// </summary>
public static class RateLimitEventManager
{
    /// <summary>
    /// Raised when a rate limit bucket is updated with fresh data from Discord.
    /// </summary>
    public static event EventHandler<RateLimitBucketUpdateEvent>? BucketUpdated;

    /// <summary>
    /// Raised when a 429 (Too Many Requests) response is received.
    /// </summary>
    public static event EventHandler<RateLimitHitEvent>? RateLimitHit;

    /// <summary>
    /// Raised when the limiter proactively waits to avoid hitting rate limits.
    /// </summary>
    public static event EventHandler<RateLimitPreEmptiveWaitEvent>? PreEmptiveWait;

    /// <summary>
    /// Raised when a request is queued due to global rate limiting.
    /// </summary>
    public static event EventHandler<RateLimitRequestQueuedEvent>? RequestQueued;

    internal static void RaiseBucketUpdated(RateLimitBucketUpdateEvent e)
        => BucketUpdated?.Invoke(null, e);

    internal static void RaiseHit(RateLimitHitEvent e)
        => RateLimitHit?.Invoke(null, e);

    internal static void RaisePreEmptiveWait(RateLimitPreEmptiveWaitEvent e)
        => PreEmptiveWait?.Invoke(null, e);

    internal static void RaiseRequestQueued(RateLimitRequestQueuedEvent e)
        => RequestQueued?.Invoke(null, e);
}
