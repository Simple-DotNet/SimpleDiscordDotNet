using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;
using SimpleDiscordNet.Gateway;
using Xunit;

namespace SimpleDiscordNet.Tests;

/// <summary>
/// Runs the real gateway client against a local WebSocket server.
/// </summary>
public class GatewayConnectionTests : IDisposable
{
    private readonly FakeGateway _server = new();
    private readonly List<string> _debug = [];
    private readonly GatewayClient _gateway;

    public GatewayConnectionTests()
    {
        _gateway = new GatewayClient("secret-token", DiscordIntents.Guilds, Json.Options, debugLog: _debug.Add, gatewayUri: _server.Uri);
    }

    public void Dispose()
    {
        _gateway.Dispose();
        _server.Dispose();
    }

    private async Task<FakeConnection> ConnectAndIdentifyAsync(int heartbeatIntervalMs = 60000)
    {
        await _gateway.ConnectAsync(CancellationToken.None);
        FakeConnection connection = await _server.NextConnectionAsync();
        await connection.HelloAsync(heartbeatIntervalMs);
        await connection.ReceiveOpAsync(2);
        return connection;
    }

    [Fact]
    public async Task Identifies_after_hello_with_the_token()
    {
        await _gateway.ConnectAsync(CancellationToken.None);
        FakeConnection connection = await _server.NextConnectionAsync();
        await connection.HelloAsync();

        JsonElement identify = await connection.ReceiveOpAsync(2);

        Assert.Equal("secret-token", identify.GetProperty("d").GetProperty("token").GetString());
    }

    [Fact]
    public async Task Debug_log_never_contains_the_token()
    {
        await ConnectAndIdentifyAsync();

        Assert.Contains(_debug, line => line.Contains("op=2"));
        Assert.DoesNotContain(_debug, line => line.Contains("secret-token"));
    }

    [Fact]
    public async Task Reconnect_request_keeps_the_session_and_resumes_at_the_resume_url()
    {
        FakeConnection first = await ConnectAndIdentifyAsync();
        await first.ReadyAsync("session-1", _server.ResumeUrl, seq: 7);
        await first.SendAsync("""{"op":7,"d":null,"s":null,"t":null}""");

        WebSocketCloseStatus? close = await first.ReceiveCloseAsync();
        FakeConnection second = await _server.NextConnectionAsync();
        await second.HelloAsync();
        JsonElement resume = await second.ReceiveOpAsync(6);

        Assert.Equal((WebSocketCloseStatus)4000, close);
        Assert.Equal("/resume/", second.Path);
        Assert.Equal("session-1", resume.GetProperty("d").GetProperty("session_id").GetString());
        Assert.Equal(7, resume.GetProperty("d").GetProperty("seq").GetInt64());
    }

    [Fact]
    public async Task Answers_a_heartbeat_request_immediately()
    {
        FakeConnection connection = await ConnectAndIdentifyAsync();

        await connection.SendAsync("""{"op":1,"d":null,"s":null,"t":null}""");
        JsonElement heartbeat = await connection.ReceiveOpAsync(1, timeoutMs: 3000);

        Assert.Equal(JsonValueKind.Null, heartbeat.GetProperty("d").ValueKind);
    }

    [Fact]
    public async Task Missing_heartbeat_acks_abort_and_reconnect()
    {
        FakeConnection first = await ConnectAndIdentifyAsync(heartbeatIntervalMs: 100);
        await first.ReadyAsync("session-1", _server.ResumeUrl);

        FakeConnection second = await _server.NextConnectionAsync();
        await second.HelloAsync();

        await second.ReceiveOpAsync(6);
    }

    [Theory]
    [InlineData(4004)]
    [InlineData(4010)]
    [InlineData(4011)]
    [InlineData(4012)]
    [InlineData(4013)]
    [InlineData(4014)]
    public async Task Fatal_close_codes_stop_reconnecting(int closeCode)
    {
        TaskCompletionSource<Exception?> disconnected = new();
        _gateway.Disconnected += (_, ex) => disconnected.TrySetResult(ex);
        FakeConnection connection = await ConnectAndIdentifyAsync();

        await connection.CloseAsync(closeCode);
        Exception? reason = await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Contains(closeCode.ToString(), reason!.Message);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _server.NextConnectionAsync(timeoutMs: 2500));
    }

    [Fact]
    public async Task A_normal_close_reconnects_without_a_long_wait()
    {
        FakeConnection connection = await ConnectAndIdentifyAsync();
        await connection.ReadyAsync("session-1", _server.ResumeUrl);
        Stopwatch stopwatch = Stopwatch.StartNew();

        await connection.CloseAsync(4000);
        await _server.NextConnectionAsync();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Took {stopwatch.Elapsed}.");
    }

    [Theory]
    [InlineData(4007)]
    [InlineData(4009)]
    public async Task Session_ending_close_codes_identify_again(int closeCode)
    {
        FakeConnection connection = await ConnectAndIdentifyAsync();
        await connection.ReadyAsync("session-1", _server.ResumeUrl);

        await connection.CloseAsync(closeCode);
        FakeConnection second = await _server.NextConnectionAsync();
        await second.HelloAsync();

        await second.ReceiveOpAsync(2);
        Assert.Equal("/", second.Path);
    }

    [Fact]
    public async Task Disconnected_fires_once_per_outage()
    {
        int disconnects = 0;
        _gateway.Disconnected += (_, _) => Interlocked.Increment(ref disconnects);
        FakeConnection connection = await ConnectAndIdentifyAsync();
        await connection.ReadyAsync("session-1", _server.ResumeUrl);

        await connection.CloseAsync(4000);
        FakeConnection second = await _server.NextConnectionAsync();
        await second.HelloAsync();
        await second.ReceiveOpAsync(6);

        Assert.Equal(1, disconnects);
    }

    [Fact]
    public async Task Connected_and_session_resumed_fire()
    {
        TaskCompletionSource connected = new();
        TaskCompletionSource resumed = new();
        _gateway.Connected += (_, _) => connected.TrySetResult();
        _gateway.SessionResumed += (_, _) => resumed.TrySetResult();
        FakeConnection connection = await ConnectAndIdentifyAsync();

        await connection.ReadyAsync("session-1", _server.ResumeUrl);
        await connected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await connection.SendAsync("""{"op":0,"s":2,"t":"RESUMED","d":{}}""");

        await resumed.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Identify_waits_for_the_shard_gate()
    {
        TaskCompletionSource<int> gateCalled = new();
        using GatewayClient shard = new("token", DiscordIntents.Guilds, Json.Options, shardId: 3, totalShards: 4, gatewayUri: _server.Uri,
            identifyGate: (id, _) => { gateCalled.TrySetResult(id); return Task.CompletedTask; });

        await shard.ConnectAsync(CancellationToken.None);
        FakeConnection connection = await _server.NextConnectionAsync();
        await connection.HelloAsync();
        await connection.ReceiveOpAsync(2);

        Assert.Equal(3, await gateCalled.Task.WaitAsync(TimeSpan.FromSeconds(1)));
    }
}
