using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using SimpleDiscordNet.Sharding.Models;

namespace SimpleDiscordNet.Sharding;

/// <summary>
/// Span-optimized HTTP client wrapper for inter-process shard coordination.
/// Uses ArrayBufferWriter and UTF8 JSON serialization for zero-copy operations.
/// Example: var client = new ShardHttpClient(); await client.PostAsync("http://host:8080/path", data);
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "ShardHttpClient uses JsonSerializerOptions configured with source-generated DiscordJsonContext for all sharding models.")]
[UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "ShardHttpClient uses JsonSerializerOptions configured with source-generated DiscordJsonContext for all sharding models.")]
internal sealed class ShardHttpClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json;

    public ShardHttpClient()
    {
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _json = new JsonSerializerOptions(Serialization.DiscordJsonContext.Default.Options)
        {
            TypeInfoResolver = Serialization.DiscordJsonContext.Default
        };
    }

    /// <summary>
    /// Sends a POST request with JSON body, returning the deserialized response.
    /// Example: var response = await client.PostAsync<Request, Response>("http://host/register", request);
    /// </summary>
    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string url, TRequest payload, CancellationToken ct = default)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            JsonSerializer.Serialize(writer, payload, _json);
        }

        using ReadOnlyMemoryContent content = new(buffer.WrittenMemory);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        using HttpResponseMessage response = await _http.PostAsync(url, content, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        byte[] responseBytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<TResponse>(responseBytes.AsSpan(), _json);
    }

    /// <summary>
    /// Sends a POST request with JSON body, no response expected.
    /// Example: await client.PostAsync("http://host/announce", announcement);
    /// </summary>
    public async Task PostAsync<TRequest>(string url, TRequest payload, CancellationToken ct = default)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            JsonSerializer.Serialize(writer, payload, _json);
        }

        using ReadOnlyMemoryContent content = new(buffer.WrittenMemory);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        using HttpResponseMessage response = await _http.PostAsync(url, content, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Sends a GET request, returning the deserialized response.
    /// Example: var state = await client.GetAsync<ClusterState>("http://host/cluster");
    /// </summary>
    public async Task<TResponse?> GetAsync<TResponse>(string url, CancellationToken ct = default)
    {
        using HttpResponseMessage response = await _http.GetAsync(url, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        byte[] responseBytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<TResponse>(responseBytes.AsSpan(), _json);
    }

    /// <summary>
    /// Sends a DELETE request with optional JSON body.
    /// Example: await client.DeleteAsync("http://host/worker/abc123");
    /// </summary>
    public async Task DeleteAsync(string url, CancellationToken ct = default)
    {
        using HttpResponseMessage response = await _http.DeleteAsync(url, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        _http.Dispose();
    }
}
