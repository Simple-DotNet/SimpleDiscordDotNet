using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Text.Json;
using SimpleDiscordNet.Logging;

namespace SimpleDiscordNet.Sharding;

/// <summary>
/// HttpListener-based HTTPS server for shard coordination endpoints.
/// Handles registration, health checks, metrics, and coordinator protocols.
/// IMPORTANT: For HTTPS support, SSL certificates must be configured using netsh http add sslcert
/// or by placing this service behind a TLS-terminating reverse proxy (nginx, Caddy, etc.).
/// Example: var server = new ShardHttpServer("https://+:8443/"); server.Start();
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "ShardHttpServer uses JsonSerializerOptions configured with source-generated DiscordJsonContext for all sharding models.")]
[UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "ShardHttpServer uses JsonSerializerOptions configured with source-generated DiscordJsonContext for all sharding models.")]
internal sealed class ShardHttpServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly JsonSerializerOptions _json;
    private readonly CancellationTokenSource _cts = new();
    private Task? _listenerTask;
    private volatile bool _disposed;

    // Route handlers
    private Func<HttpListenerContext, Task>? _registerHandler;
    private Func<HttpListenerContext, Task>? _healthHandler;
    private Func<HttpListenerContext, Task>? _metricsHandler;
    private Func<HttpListenerContext, Task>? _assignmentHandler;
    private Func<HttpListenerContext, Task>? _successionHandler;
    private Func<HttpListenerContext, Task>? _migrationHandler;
    private Func<HttpListenerContext, Task>? _clusterStateHandler;
    private Func<HttpListenerContext, Task>? _resumptionHandler;
    private Func<HttpListenerContext, Task>? _handoffHandler;
    private Func<HttpListenerContext, Task>? _resumedAnnouncementHandler;

    public ShardHttpServer(string prefix)
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add(prefix);
        _json = new JsonSerializerOptions(Serialization.DiscordJsonContext.Default.Options)
        {
            TypeInfoResolver = Serialization.DiscordJsonContext.Default
        };
    }

    /// <summary>
    /// Registers a handler for POST /register endpoint.
    /// Example: server.OnRegister(async ctx => { ... });
    /// </summary>
    public void OnRegister(Func<HttpListenerContext, Task> handler) => _registerHandler = handler;

    /// <summary>
    /// Registers a handler for GET /health endpoint.
    /// Example: server.OnHealth(async ctx => { ... });
    /// </summary>
    public void OnHealth(Func<HttpListenerContext, Task> handler) => _healthHandler = handler;

    /// <summary>
    /// Registers a handler for POST /metrics endpoint.
    /// Example: server.OnMetrics(async ctx => { ... });
    /// </summary>
    public void OnMetrics(Func<HttpListenerContext, Task> handler) => _metricsHandler = handler;

    /// <summary>
    /// Registers a handler for POST /assignment endpoint.
    /// Example: server.OnAssignment(async ctx => { ... });
    /// </summary>
    public void OnAssignment(Func<HttpListenerContext, Task> handler) => _assignmentHandler = handler;

    /// <summary>
    /// Registers a handler for POST /succession endpoint.
    /// Example: server.OnSuccession(async ctx => { ... });
    /// </summary>
    public void OnSuccession(Func<HttpListenerContext, Task> handler) => _successionHandler = handler;

    /// <summary>
    /// Registers a handler for POST /migrate endpoint.
    /// Example: server.OnMigration(async ctx => { ... });
    /// </summary>
    public void OnMigration(Func<HttpListenerContext, Task> handler) => _migrationHandler = handler;

    /// <summary>
    /// Registers a handler for GET /cluster endpoint.
    /// Example: server.OnClusterState(async ctx => { ... });
    /// </summary>
    public void OnClusterState(Func<HttpListenerContext, Task> handler) => _clusterStateHandler = handler;

    /// <summary>
    /// Registers a handler for POST /coordinator/resume endpoint.
    /// Example: server.OnResumption(async ctx => { ... });
    /// </summary>
    public void OnResumption(Func<HttpListenerContext, Task> handler) => _resumptionHandler = handler;

    /// <summary>
    /// Registers a handler for POST /coordinator/handoff endpoint.
    /// Example: server.OnHandoff(async ctx => { ... });
    /// </summary>
    public void OnHandoff(Func<HttpListenerContext, Task> handler) => _handoffHandler = handler;

    /// <summary>
    /// Registers a handler for POST /coordinator/resumed endpoint.
    /// Example: server.OnResumedAnnouncement(async ctx => { ... });
    /// </summary>
    public void OnResumedAnnouncement(Func<HttpListenerContext, Task> handler) => _resumedAnnouncementHandler = handler;

    /// <summary>
    /// Starts the HTTP listener and begins processing requests.
    /// Example: server.Start();
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _listener.Start();
        _listenerTask = Task.Run(ListenLoop, _cts.Token);
        // Server started (no logging needed at this level)
    }

    private async Task ListenLoop()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                HttpListenerContext context = await _listener.GetContextAsync().ConfigureAwait(false);
                _ = Task.Run(() => HandleRequestAsync(context), _cts.Token);
            }
            catch (HttpListenerException) when (_cts.Token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                // Swallow listener errors (handled by callers)
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        try
        {
            string path = context.Request.Url?.AbsolutePath ?? string.Empty;
            string method = context.Request.HttpMethod;

            Func<HttpListenerContext, Task>? handler = (method, path) switch
            {
                ("POST", "/register") => _registerHandler,
                ("GET", "/health") => _healthHandler,
                ("POST", "/metrics") => _metricsHandler,
                ("POST", "/assignment") => _assignmentHandler,
                ("POST", "/succession") => _successionHandler,
                ("POST", "/migrate") => _migrationHandler,
                ("GET", "/cluster") => _clusterStateHandler,
                ("POST", "/coordinator/resume") => _resumptionHandler,
                ("POST", "/coordinator/handoff") => _handoffHandler,
                ("POST", "/coordinator/resumed") => _resumedAnnouncementHandler,
                _ => null
            };

            if (handler != null)
            {
                await handler(context).ConfigureAwait(false);
            }
            else
            {
                await RespondAsync(context, 404, new HttpErrorResponse { error = "Not Found" }).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            // Swallow request errors
            try
            {
                await RespondAsync(context, 500, new HttpErrorResponse { error = ex.Message }).ConfigureAwait(false);
            }
            catch { /* Best effort */ }
        }
    }

    /// <summary>
    /// Reads JSON request body and deserializes to type T.
    /// Example: var request = await ReadJsonAsync<WorkerRegistrationRequest>(context);
    /// </summary>
    public async Task<T?> ReadJsonAsync<T>(HttpListenerContext context)
    {
        using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
        var json = await reader.ReadToEndAsync().ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(json, _json);
    }

    /// <summary>
    /// Sends JSON response with specified status code.
    /// Example: await RespondAsync(context, 200, new { success = true });
    /// </summary>
    public async Task RespondAsync<T>(HttpListenerContext context, int statusCode, T data)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            JsonSerializer.Serialize(writer, data, _json);
        }

        context.Response.ContentLength64 = buffer.WrittenCount;
        await context.Response.OutputStream.WriteAsync(buffer.WrittenMemory, _cts.Token).ConfigureAwait(false);
        context.Response.Close();
    }

    /// <summary>
    /// Sends empty response with specified status code.
    /// Example: await RespondAsync(context, 204);
    /// </summary>
    public static Task RespondAsync(HttpListenerContext context, int statusCode)
    {
        context.Response.StatusCode = statusCode;
        context.Response.Close();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();
        _listener.Stop();
        _listener.Close();
        _cts.Dispose();

        // Server stopped (no logging needed at this level)
    }
}
