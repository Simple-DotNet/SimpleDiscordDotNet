using System;
using System.Text.Json;

namespace SimpleDiscordNet.Gateway;

internal sealed class Identify
{
    public int op { get; set; } = 2; // Identify opcode
    public IdentifyPayload d { get; set; } = new();
}

internal sealed class IdentifyPayload
{
    public string token { get; set; } = string.Empty;
    public IdentifyConnectionProperties properties { get; set; } = new();
    public int intents { get; set; }
    public int[]? shard { get; set; }
}

internal sealed class IdentifyConnectionProperties
{
    public string os { get; set; } = Environment.OSVersion.Platform.ToString();
    public string browser { get; set; } = "SimpleDiscordNet";
    public string device { get; set; } = "SimpleDiscordNet";
}

internal sealed class Heartbeat
{
    public int op { get; set; } = 1; // Heartbeat opcode
    public object? d { get; set; }
}

internal sealed class Resume
{
    public int op { get; set; } = 6; // Resume opcode
    public ResumePayload d { get; set; } = new();
}

internal sealed class ResumePayload
{
    public string token { get; set; } = string.Empty;
    public string session_id { get; set; } = string.Empty;
    public long seq { get; set; }
}

internal sealed class RequestGuildMembers
{
    public int op { get; set; } = 8; // Request Guild Members opcode
    public RequestGuildMembersPayload d { get; set; } = new();
}

internal sealed class RequestGuildMembersPayload
{
    public string guild_id { get; set; } = string.Empty;
    public string query { get; set; } = string.Empty; // empty string = all members
    public int limit { get; set; } = 0; // 0 = all members
}

internal sealed class GatewayPayload
{
    public int op { get; set; }
    public string? t { get; set; }
    public long? s { get; set; }
    public JsonElement d { get; set; }
}

internal sealed class UpdatePresence
{
    public int op { get; set; } = 3;
    public UpdatePresencePayload d { get; set; } = new();
}

internal sealed class UpdatePresencePayload
{
    public long? since { get; set; }
    public BotActivity[]? activities { get; set; }
    public string status { get; set; } = "online";
    public bool afk { get; set; }
}

public sealed class BotActivity
{
    public string name { get; set; } = string.Empty;
    public int type { get; set; }
    public string? url { get; set; }

    public BotActivity() { }

    public BotActivity(string name, ActivityType type, string? url = null)
    {
        this.name = name;
        this.type = (int)type;
        this.url = url;
    }

    public static BotActivity Game(string name) => new() { name = name, type = (int)ActivityType.Game };
    public static BotActivity Watching(string name) => new() { name = name, type = (int)ActivityType.Watching };
    public static BotActivity Listening(string name) => new() { name = name, type = (int)ActivityType.Listening };
    public static BotActivity Streaming(string name, string url) => new() { name = name, type = (int)ActivityType.Streaming, url = url };
    public static BotActivity Competing(string name) => new() { name = name, type = (int)ActivityType.Competing };
}

public enum PresenceStatus
{
    Online = 0,
    Idle = 1,
    DoNotDisturb = 2,
    Invisible = 3
}

public enum ActivityType
{
    Game = 0,
    Streaming = 1,
    Listening = 2,
    Watching = 3,
    Competing = 5
}
