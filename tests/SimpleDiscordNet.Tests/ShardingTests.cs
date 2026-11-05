using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using SimpleDiscordNet.Sharding;
using SimpleDiscordNet.Sharding.Models;
using Xunit;

namespace SimpleDiscordNet.Tests;

public class ShardAssignerTests
{
    private static PeerNode Worker(string id, params int[] shards)
    {
        PeerNode peer = new(id, $"http://{id}");
        peer.AssignedShards.AddRange(shards);
        return peer;
    }

    private static int[] AllShards(params PeerNode[] peers)
        => peers.SelectMany(p => p.GetShardsSnapshot()).Order().ToArray();

    [Fact]
    public void First_worker_gets_every_shard()
    {
        PeerNode worker = Worker("a");

        List<ShardMigrationRequest> moves = ShardAssigner.AssignNewWorker([worker], worker, 8);

        Assert.Equal(Enumerable.Range(0, 8), worker.GetShardsSnapshot());
        Assert.Empty(moves);
    }

    [Fact]
    public void Second_worker_takes_a_fair_share_and_the_old_owner_is_told()
    {
        PeerNode first = Worker("a", 0, 1, 2, 3, 4, 5, 6, 7);
        PeerNode second = Worker("b");

        List<ShardMigrationRequest> moves = ShardAssigner.AssignNewWorker([first, second], second, 8);

        Assert.Equal(4, second.GetShardsSnapshot().Length);
        Assert.Equal(4, first.GetShardsSnapshot().Length);
        Assert.Equal(4, moves.Count);
        Assert.All(moves, m => Assert.Equal("a", m.FromNode));
        Assert.Equal(Enumerable.Range(0, 8), AllShards(first, second));
    }

    [Fact]
    public void Max_shards_is_respected()
    {
        PeerNode worker = Worker("a");
        worker.MaxShards = 3;

        ShardAssigner.AssignNewWorker([worker], worker, 8);

        Assert.Equal(3, worker.GetShardsSnapshot().Length);
    }

    [Fact]
    public void Unassigned_shards_go_to_the_next_worker()
    {
        PeerNode first = Worker("a", 0, 1);
        first.MaxShards = 2;
        PeerNode second = Worker("b");

        ShardAssigner.AssignNewWorker([first, second], second, 4);

        Assert.Equal([2, 3], second.GetShardsSnapshot());
    }

    [Fact]
    public void Rebalancing_never_loses_or_duplicates_a_shard()
    {
        PeerNode first = Worker("a");
        ShardAssigner.AssignNewWorker([first], first, 16);
        PeerNode second = Worker("b");
        ShardAssigner.AssignNewWorker([first, second], second, 16);
        PeerNode third = Worker("c");

        ShardAssigner.AssignNewWorker([first, second, third], third, 16);

        Assert.Equal(Enumerable.Range(0, 16), AllShards(first, second, third));
    }

    [Fact]
    public void Failed_shards_go_to_the_least_loaded_workers()
    {
        PeerNode busy = Worker("a", 0, 1, 2);
        PeerNode idle = Worker("b");

        Dictionary<PeerNode, List<int>> assigned = ShardAssigner.ReassignShards([3, 4, 5], [busy, idle]);

        Assert.Equal([3, 4, 5], assigned[idle]);
        Assert.DoesNotContain(busy, assigned.Keys);
    }

    [Fact]
    public void Failed_shards_wait_when_no_worker_has_capacity()
    {
        PeerNode full = Worker("a", 0);
        full.MaxShards = 1;

        Assert.Empty(ShardAssigner.ReassignShards([1], [full]));
    }
}

public class ShardUrlTests
{
    [Theory]
    [InlineData("http://+:8080/", "http://box:8080")]
    [InlineData("http://*:8080", "http://box:8080")]
    [InlineData("https://+:8443/", "https://box:8443")]
    [InlineData("http://10.0.0.5:8080/", "http://10.0.0.5:8080")]
    public void Wildcard_hosts_become_a_callable_address(string listenUrl, string expected)
    {
        Assert.Equal(expected, ShardUrl.ToAdvertised(listenUrl, "box"));
    }

    [Fact]
    public void Normalize_prevents_a_double_slash()
    {
        Assert.Equal("http://host:8080/register", ShardUrl.Normalize("http://host:8080/") + "/register");
    }

    [Fact]
    public void Listener_prefix_ends_with_one_slash()
    {
        Assert.Equal("http://+:8080/", ShardUrl.ToListenerPrefix("http://+:8080"));
    }
}

public class IdentifyRateLimiterTests
{
    private static async Task<long> TimeAsync(IdentifyRateLimiter limiter, int shardId)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        await limiter.WaitAsync(shardId, CancellationToken.None);
        return stopwatch.ElapsedMilliseconds;
    }

    [Fact]
    public async Task First_identify_does_not_wait()
    {
        Assert.True(await TimeAsync(new IdentifyRateLimiter(1), 0) < 500);
    }

    [Fact]
    public async Task Shards_in_different_buckets_identify_together()
    {
        IdentifyRateLimiter limiter = new(maxConcurrency: 2);
        await limiter.WaitAsync(0, CancellationToken.None);

        Assert.True(await TimeAsync(limiter, 1) < 500);
    }

    [Fact]
    public async Task Shards_in_the_same_bucket_are_spaced_out()
    {
        DiscordTime time = new();
        IdentifyRateLimiter limiter = new(maxConcurrency: 1, time);
        await limiter.WaitAsync(0, CancellationToken.None);

        Task second = limiter.WaitAsync(1, CancellationToken.None);
        await Task.Delay(50);
        Assert.False(second.IsCompleted);

        time.Advance(TimeSpan.FromSeconds(5));
        await second.WaitAsync(TimeSpan.FromSeconds(2));
    }

    // A minimal manual clock: timers fire when the test advances time
    private sealed class DiscordTime : TimeProvider
    {
        private readonly List<(DateTimeOffset Due, TimerCallback Callback, object? State)> _timers = [];
        private DateTimeOffset _now = DateTimeOffset.UnixEpoch.AddYears(50);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by)
        {
            _now += by;
            List<(DateTimeOffset Due, TimerCallback Callback, object? State)> due;
            lock (_timers)
            {
                due = _timers.Where(t => t.Due <= _now).ToList();
                _timers.RemoveAll(t => t.Due <= _now);
            }
            foreach ((_, TimerCallback callback, object? state) in due)
            {
                callback(state);
            }
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            lock (_timers)
            {
                _timers.Add((_now + dueTime, callback, state));
            }
            return new NoOpTimer();
        }

        private sealed class NoOpTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}

public class ClusterAuthTests : IDisposable
{
    private readonly string _secret = ClusterAuth.DeriveSecret("bot-token");
    private readonly ShardHttpServer _server;
    private readonly string _url;

    public ClusterAuthTests()
    {
        TcpListener probe = new(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        _url = $"http://localhost:{port}";
        _server = new ShardHttpServer(_url, _secret);
        _server.OnHealth(context => _server.RespondAsync(context, 200, new SuccessResponse { success = true }));
        _server.Start();
    }

    public void Dispose() => _server.Dispose();

    [Fact]
    public void Secret_is_stable_and_hides_the_token()
    {
        Assert.Equal(_secret, ClusterAuth.DeriveSecret("bot-token"));
        Assert.NotEqual(_secret, ClusterAuth.DeriveSecret("other-token"));
        Assert.DoesNotContain("bot-token", _secret);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong")]
    public void Wrong_secrets_are_rejected(string? provided)
    {
        Assert.False(ClusterAuth.IsValid(provided, _secret));
    }

    [Fact]
    public async Task Requests_without_the_secret_get_401()
    {
        using HttpClient client = new();

        HttpResponseMessage response = await client.GetAsync($"{_url}/health");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Requests_with_the_wrong_secret_get_401()
    {
        using ShardHttpClient client = new(ClusterAuth.DeriveSecret("other-token"));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync<SuccessResponse>($"{_url}/health"));
    }

    [Fact]
    public async Task Requests_with_the_secret_succeed()
    {
        using ShardHttpClient client = new(_secret);

        SuccessResponse? response = await client.GetAsync<SuccessResponse>($"{_url}/health");

        Assert.True(response!.success);
    }
}

public class GatewayLimitsTests
{
    [Fact]
    public void Reads_shards_and_max_concurrency()
    {
        Assert.Equal((4, 16), ShardCoordinator.ParseGatewayLimits("""{"url":"wss://x","shards":4,"session_start_limit":{"total":1000,"remaining":999,"reset_after":1,"max_concurrency":16}}"""));
    }

    [Fact]
    public void Max_concurrency_defaults_to_one()
    {
        Assert.Equal((2, 1), ShardCoordinator.ParseGatewayLimits("""{"shards":2}"""));
    }
}
