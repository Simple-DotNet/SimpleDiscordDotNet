using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using SimpleDiscordNet;
using SimpleDiscordNet.Logging;
using SimpleDiscordNet.Rest;

namespace SimpleDiscordNet.Tests;

internal static class Json
{
    public static JsonSerializerOptions Options { get; } = new DiscordBotOptions().JsonOptions;

    public static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    public const string User = """{"id":"80351110224678912","username":"Nelly","discriminator":"0","global_name":"Nell","avatar":"8342729096ea3675442027381ff50dfe","bot":true,"public_flags":64,"accent_color":16711680}""";
    public const string Role = """{"id":"41771983423143936","name":"Mods","color":3447003,"hoist":true,"position":1,"permissions":"66321471","managed":false,"mentionable":false}""";
    public const string Member = "{\"user\":" + User + ",\"nick\":\"Nick\",\"roles\":[\"41771983423143936\"],\"joined_at\":\"2015-04-26T06:26:56.936000+00:00\",\"deaf\":false,\"mute\":false,\"flags\":0,\"pending\":false,\"permissions\":\"8\"}";
    public const string Channel = """{"id":"41771983423143937","guild_id":"41771983423143938","name":"general","type":0,"position":6,"permission_overwrites":[{"id":"41771983423143936","type":0,"allow":"1024","deny":"2048"}],"nsfw":true,"topic":"chat","parent_id":"399942396007890945"}""";
    public const string Guild = "{\"id\":\"197038439483310086\",\"name\":\"Testers\",\"icon\":\"abc\",\"owner_id\":\"73193882359173120\",\"afk_channel_id\":null,\"afk_timeout\":300,\"verification_level\":3,\"member_count\":12,\"features\":[\"COMMUNITY\"],\"roles\":[" + Role + "],\"emojis\":[{\"id\":\"1\",\"name\":\"x\",\"roles\":[],\"animated\":false}],\"premium_tier\":3,\"preferred_locale\":\"en-US\"}";
    public const string Message = "{\"id\":\"334385199974967042\",\"channel_id\":\"290926798999357250\",\"author\":" + User + ",\"content\":\"hi\",\"timestamp\":\"2017-07-11T17:27:07.299000+00:00\",\"edited_timestamp\":null,\"tts\":false,\"mention_everyone\":false,\"mentions\":[],\"mention_roles\":[\"41771983423143936\"],\"attachments\":[{\"id\":\"1\",\"filename\":\"a.png\",\"size\":10,\"url\":\"https://x/y\"}],\"embeds\":[],\"pinned\":false,\"type\":0}";
}

/// <summary>Records outgoing HTTP requests and answers them with a canned response.</summary>
internal sealed class StubHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; }
        = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };

    public List<HttpRequestMessage> Requests { get; } = [];
    public string? LastBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // In this order, a test that sees a new request can read its body
        LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(request);
        return Respond(request);
    }
}

internal sealed class RestFixture : IDisposable
{
    public StubHandler Handler { get; } = new();
    public NativeLogger Logger { get; } = new();
    public List<string> Logs { get; } = [];
    public RestClient Rest { get; }

    public RestFixture()
    {
        Logger.Logged += (_, message) => Logs.Add(message.Message);
        Rest = new RestClient(new HttpClient(Handler), "test-token", Json.Options, Logger, new RateLimiter());
    }

    public void Dispose() => Rest.Dispose();
}

/// <summary>A local WebSocket server that speaks just enough of the gateway protocol for tests.</summary>
internal sealed class FakeGateway : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Channel<FakeConnection> _connections = Channel.CreateUnbounded<FakeConnection>();
    private readonly CancellationTokenSource _cts = new();
    private readonly int _port;

    public FakeGateway()
    {
        TcpListener probe = new(IPAddress.Loopback, 0);
        probe.Start();
        _port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        _listener.Prefixes.Add($"http://localhost:{_port}/");
        _listener.Start();
        _ = AcceptLoopAsync();
    }

    public Uri Uri => new($"ws://localhost:{_port}/?v=10&encoding=json");
    public string ResumeUrl => $"ws://localhost:{_port}/resume";

    private async Task AcceptLoopAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                HttpListenerContext context = await _listener.GetContextAsync();
                HttpListenerWebSocketContext ws = await context.AcceptWebSocketAsync(null);
                await _connections.Writer.WriteAsync(new FakeConnection(ws.WebSocket, context.Request.Url!.AbsolutePath));
            }
        }
        catch (Exception) when (_cts.IsCancellationRequested)
        {
            // Shutting down
        }
        catch (HttpListenerException)
        {
            // Shutting down
        }
        catch (ObjectDisposedException)
        {
            // Shutting down
        }
    }

    public async Task<FakeConnection> NextConnectionAsync(int timeoutMs = 10000)
    {
        using CancellationTokenSource timeout = new(timeoutMs);
        return await _connections.Reader.ReadAsync(timeout.Token);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Close();
        _cts.Dispose();
    }
}

internal sealed class FakeConnection(WebSocket socket, string path)
{
    public string Path { get; } = path;
    public WebSocket Socket { get; } = socket;

    public Task SendAsync(string json)
        => Socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, CancellationToken.None);

    public Task HelloAsync(int heartbeatIntervalMs = 60000)
        => SendAsync($$"""{"op":10,"d":{"heartbeat_interval":{{heartbeatIntervalMs}}},"s":null,"t":null}""");

    public Task ReadyAsync(string sessionId, string resumeUrl, int seq = 1)
        => SendAsync($$"""{"op":0,"s":{{seq}},"t":"READY","d":{"session_id":"{{sessionId}}","resume_gateway_url":"{{resumeUrl}}",""" + """ "user":{"id":"1","username":"bot"}}}""");

    public Task CloseAsync(int code)
        => Socket.CloseOutputAsync((WebSocketCloseStatus)code, "test", CancellationToken.None);

    /// <summary>Reads the next frame, returning its opcode, or -1 when the client closed the socket.</summary>
    public async Task<(int Op, JsonElement Payload, WebSocketCloseStatus? Close)> ReceiveAsync(int timeoutMs = 10000)
    {
        using CancellationTokenSource timeout = new(timeoutMs);
        byte[] buffer = new byte[16384];
        using MemoryStream message = new();
        while (true)
        {
            WebSocketReceiveResult result = await Socket.ReceiveAsync(buffer, timeout.Token);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return (-1, default, result.CloseStatus);
            }

            message.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                JsonElement root = JsonDocument.Parse(message.ToArray()).RootElement;
                return (root.GetProperty("op").GetInt32(), root, null);
            }
        }
    }

    /// <summary>Reads frames until one with the given opcode arrives.</summary>
    public async Task<JsonElement> ReceiveOpAsync(int op, int timeoutMs = 10000)
    {
        while (true)
        {
            (int received, JsonElement payload, _) = await ReceiveAsync(timeoutMs);
            if (received == op) return payload;
            if (received == -1) throw new InvalidOperationException($"Socket closed while waiting for op {op}.");
        }
    }

    /// <summary>Reads frames until the client closes, returning the close status it sent.</summary>
    public async Task<WebSocketCloseStatus?> ReceiveCloseAsync(int timeoutMs = 10000)
    {
        while (true)
        {
            (int received, _, WebSocketCloseStatus? close) = await ReceiveAsync(timeoutMs);
            if (received == -1) return close;
        }
    }
}
