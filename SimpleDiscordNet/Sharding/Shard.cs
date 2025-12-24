using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using SimpleDiscordNet.Gateway;
using SimpleDiscordNet.Logging;

namespace SimpleDiscordNet.Sharding;

/// <summary>
/// Represents a single shard connection to Discord's gateway.
/// Wraps a GatewayClient and tracks shard-specific metrics.
/// </summary>
internal sealed class Shard : IDisposable
{
    private readonly int _shardId;
    private readonly int _totalShards;
    private readonly string _token;
    private readonly DiscordIntents _intents;
    private readonly JsonSerializerOptions _json;
    private readonly NativeLogger _logger;
    private readonly GatewayClient _gateway;
    private readonly ConcurrentDictionary<string, bool> _guilds = new();

    private volatile ShardStatus _status = ShardStatus.Disconnected;
    private volatile int _latency;
    private volatile int _eventsPerSecond;
    private volatile int _commandsPerSecond;

    private readonly Stopwatch _eventCounter = Stopwatch.StartNew();
    private int _eventCount;
    private int _commandCount;

    /// <summary>
    /// Creates a new shard wrapper.
    /// Example: new Shard(0, 4, token, intents, json, logger)
    /// </summary>
    public Shard(int shardId, int totalShards, string token, DiscordIntents intents, JsonSerializerOptions json, NativeLogger logger)
    {
        _shardId = shardId;
        _totalShards = totalShards;
        _token = token;
        _intents = intents;
        _json = json;
        _logger = logger;
        _gateway = new GatewayClient(token, intents, json, shardId, totalShards);

        WireEvents();
    }

    public int Id => _shardId;
    public int Total => _totalShards;
    public ShardStatus Status => _status;
    public int Latency => _latency;
    public int GuildCount => _guilds.Count;
    public int EventsPerSecond => _eventsPerSecond;
    public int CommandsPerSecond => _commandsPerSecond;
    public IReadOnlyList<string> GuildIds => _guilds.Keys.ToArray();
    public GatewayClient Gateway => _gateway;

    /// <summary>
    /// Connects this shard to Discord's gateway.
    /// Automatically includes shard info [shard_id, total_shards] in IDENTIFY payload.
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct)
    {
        _status = ShardStatus.Connecting;
        _logger.Log(LogLevel.Information, $"Shard {_shardId}/{_totalShards}: Connecting to Discord gateway");

        try
        {
            await _gateway.ConnectAsync(ct).ConfigureAwait(false);
            _status = ShardStatus.Connected;
        }
        catch (Exception ex)
        {
            _status = ShardStatus.Failed;
            _logger.Log(LogLevel.Error, $"Shard {_shardId}/{_totalShards}: Connection failed", ex);
            throw;
        }
    }

    /// <summary>
    /// Disconnects this shard from Discord's gateway.
    /// </summary>
    public async Task DisconnectAsync()
    {
        _status = ShardStatus.Disconnected;
        _logger.Log(LogLevel.Information, $"Shard {_shardId}/{_totalShards}: Disconnecting");
        await _gateway.DisconnectAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Registers a guild with this shard.
    /// Called when GUILD_CREATE is received.
    /// </summary>
    public void AddGuild(string guildId)
    {
        _guilds.TryAdd(guildId, true);
    }

    /// <summary>
    /// Unregisters a guild from this shard.
    /// Called when GUILD_DELETE is received.
    /// </summary>
    public void RemoveGuild(string guildId)
    {
        _guilds.TryRemove(guildId, out _);
    }

    /// <summary>
    /// Increments event counter for metrics.
    /// Called on every gateway event.
    /// </summary>
    public void IncrementEventCount()
    {
        Interlocked.Increment(ref _eventCount);
    }

    /// <summary>
    /// Increments command counter for metrics.
    /// Called on every command invocation.
    /// </summary>
    public void IncrementCommandCount()
    {
        Interlocked.Increment(ref _commandCount);
    }

    /// <summary>
    /// Updates latency from heartbeat ack.
    /// </summary>
    public void UpdateLatency(int latencyMs)
    {
        _latency = latencyMs;
    }

    /// <summary>
    /// Returns current shard information for public API.
    /// </summary>
    public ShardInfo ToInfo(string? hostUrl = null)
    {
        return new ShardInfo
        {
            Id = _shardId,
            Total = _totalShards,
            Status = _status,
            GuildCount = _guilds.Count,
            Latency = _latency,
            GuildIds = _guilds.Keys.ToArray(),
            EventsPerSecond = _eventsPerSecond,
            CommandsPerSecond = _commandsPerSecond,
            HostUrl = hostUrl
        };
    }

    private void WireEvents()
    {
        _gateway.Connected += (_, _) =>
        {
            _status = ShardStatus.Connected;
            _logger.Log(LogLevel.Information, $"Shard {_shardId}/{_totalShards}: Connected");
        };

        _gateway.Disconnected += (_, ex) =>
        {
            _status = ex == null ? ShardStatus.Disconnected : ShardStatus.Reconnecting;
            _logger.Log(LogLevel.Warning, $"Shard {_shardId}/{_totalShards}: Disconnected", ex);
        };

        // Update metrics every second
        _ = Task.Run(async () =>
        {
            while (_status != ShardStatus.Disconnected && _status != ShardStatus.Failed)
            {
                await Task.Delay(1000).ConfigureAwait(false);

                if (_eventCounter.ElapsedMilliseconds >= 1000)
                {
                    _eventsPerSecond = Interlocked.Exchange(ref _eventCount, 0);
                    _commandsPerSecond = Interlocked.Exchange(ref _commandCount, 0);
                    _eventCounter.Restart();
                }
            }
        });
    }

    public void Dispose()
    {
        _gateway?.Dispose();
        _guilds.Clear();
    }
}
