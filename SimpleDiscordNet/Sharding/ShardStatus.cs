namespace SimpleDiscordNet.Sharding;

/// <summary>
/// Current connection status of a shard.
/// </summary>
public enum ShardStatus
{
    /// <summary>Shard is not connected to Discord gateway.</summary>
    Disconnected = 0,

    /// <summary>Shard is attempting to connect to Discord gateway.</summary>
    Connecting = 1,

    /// <summary>Shard is connected and performing handshake.</summary>
    Connected = 2,

    /// <summary>Shard is fully ready and operational (received READY event).</summary>
    Ready = 3,

    /// <summary>Shard is reconnecting after a disconnect.</summary>
    Reconnecting = 4,

    /// <summary>Shard failed to connect and is not retrying.</summary>
    Failed = 5
}
