using System.Text.Json;
using SimpleDiscordNet.Logging;
using SimpleDiscordNet.Sharding;

namespace SimpleDiscordNet;

/// <summary>
/// Configuration options for constructing <see cref="DiscordBot"/> via DI or factories.
/// </summary>
public sealed record DiscordBotOptions
{
    /// <summary>Bot token. Required.</summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>Gateway intents. Defaults to Guilds | GuildMessages | DirectMessages | MessageContent.</summary>
    public DiscordIntents Intents { get; init; } = DiscordIntents.Guilds | DiscordIntents.GuildMessages | DiscordIntents.DirectMessages | DiscordIntents.MessageContent;

    /// <summary>System.Text.Json options. Defaults to source-generated DiscordJsonContext with case-insensitive props.</summary>
    public JsonSerializerOptions JsonOptions { get; init; } = new(Serialization.DiscordJsonContext.Default.Options)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        TypeInfoResolver = Serialization.DiscordJsonContext.Default
    };

    /// <summary>Optional time provider for testing or customization.</summary>
    public TimeProvider? TimeProvider { get; init; }

    /// <summary>Preload caches on start using REST calls.</summary>
    public bool PreloadGuilds { get; init; } = true;
    public bool PreloadChannels { get; init; } = true;
    public bool PreloadMembers { get; init; } = false;

    /// <summary>
    /// When enabled (default), the bot will automatically load complete guild data after receiving GUILD_CREATE.
    /// This includes fetching missing channels (if not in payload), requesting full member list (via gateway chunking),
    /// and ensuring all roles/emojis are cached. A GuildReady event will fire when loading is complete.
    /// Disable this if you want to manually control data loading or only need partial guild data.
    /// Requires appropriate intents (GuildMembers for full member list).
    /// </summary>
    public bool AutoLoadFullGuildData { get; init; } = true;

    /// <summary>
    /// When enabled, the bot will immediately synchronize all discovered slash commands
    /// to the specified DevelopmentGuildIds on Start/StartAsync. This uses per-guild
    /// commands (PUT /applications/{app}/guilds/{guild}/commands) which propagate
    /// instantly and are ideal for development.
    /// </summary>
    public bool DevelopmentMode { get; init; } = false;

    /// <summary>
    /// Guild ids that should receive immediate slash-command sync when DevelopmentMode is enabled.
    /// </summary>
    public List<string> DevelopmentGuildIds { get; init; } = [];

    /// <summary>
    /// Optional log sink to mirror internal logs to your application (e.g., to ILogger).
    /// This is in addition to the global DiscordEvents.Log event.
    /// </summary>
    public Action<LogMessage>? LogSink { get; init; }

    /// <summary>
    /// Minimum log level to emit from the library. Messages below this level are ignored.
    /// Defaults to <see cref="LogLevel.Trace"/> (emit everything) to preserve previous behavior.
    /// </summary>
    public LogLevel MinimumLogLevel { get; init; } = LogLevel.Trace;

    /// <summary>
    /// Sharding mode: SingleProcess (all shards in one process) or Distributed (shards across multiple machines).
    /// Example: ShardMode = ShardMode.Distributed
    /// </summary>
    public ShardMode ShardMode { get; init; } = ShardMode.SingleProcess;

    /// <summary>
    /// For Distributed sharding, the coordinator URL (e.g., "http://192.168.1.100:8080/").
    /// Workers connect to this URL for registration and coordination.
    /// Example: CoordinatorUrl = "http://192.168.1.100:8080/"
    /// </summary>
    public string? CoordinatorUrl { get; init; }

    /// <summary>
    /// For Distributed sharding, the URL this worker should listen on (e.g., "http://+:8080/").
    /// Required when running as a distributed worker.
    /// Example: WorkerListenUrl = "http://+:8080/"
    /// </summary>
    public string? WorkerListenUrl { get; init; }

    /// <summary>
    /// For Distributed sharding, unique identifier for this worker.
    /// Auto-generated if not specified using machine name + GUID.
    /// Example: WorkerId = "worker-1"
    /// </summary>
    public string? WorkerId { get; init; }

    /// <summary>
    /// For Distributed sharding, whether this instance is the original coordinator.
    /// Set to true on the designated coordinator machine.
    /// Example: IsOriginalCoordinator = true
    /// </summary>
    public bool IsOriginalCoordinator { get; init; } = false;

    /// <summary>
    /// For SingleProcess sharding, the specific shard ID to run (0-based).
    /// If null, defaults to shard 0.
    /// Example: ShardId = 0
    /// </summary>
    public int? ShardId { get; init; }

    /// <summary>
    /// For SingleProcess sharding, the total number of shards.
    /// If null, defaults to 1 (no sharding).
    /// Example: TotalShards = 4
    /// </summary>
    public int? TotalShards { get; init; }
}
