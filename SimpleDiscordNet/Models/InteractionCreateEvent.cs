using SimpleDiscordNet.Primitives;

namespace SimpleDiscordNet.Models;

public enum InteractionType
{
    Ping = 1,
    ApplicationCommand = 2,
    MessageComponent = 3,
    ApplicationCommandAutocomplete = 4,
    ModalSubmit = 5
}

public sealed record InteractionCreateEvent
{
    public required string Id { get; init; }
    public required string Token { get; init; }
    public required string ApplicationId { get; init; }
    public InteractionType Type { get; init; }
    public string? GuildId { get; init; }
    public string? ChannelId { get; init; }
    public Author? Author { get; init; }
    // Present when interaction is in a guild
    public Entities.DiscordMember? Member { get; init; }
    // Guild object if available (resolved from cache)
    public Entities.DiscordGuild? Guild { get; init; }
    // Present when Type == ApplicationCommand
    public ApplicationCommandData? Data { get; init; }
    // Present when Type == MessageComponent
    public MessageComponentData? Component { get; init; }
    // Present when Type == ModalSubmit
    public ModalSubmitData? Modal { get; init; }

    /// <summary>The permissions the bot has where this interaction happened.</summary>
    public ulong? AppPermissions { get; init; }
    // The shard that received this interaction. Null when the bot isn't sharded.
    public int? ShardId { get; init; }
}

public sealed record ApplicationCommandData
{
    // Top-level command name
    public required string Name { get; init; }
    // When present, this is the subcommand group name under the top-level command
    public string? SubcommandGroup { get; init; }
    // When present, this is the subcommand name (direct or under SubcommandGroup)
    public string? Subcommand { get; init; }
    public IReadOnlyList<InteractionOption> Options { get; init; } = [];
    // The user or message a context menu command was used on
    public string? TargetId { get; init; }
    // Users, members, roles, channels, messages and attachments referenced by this command
    public ResolvedData? Resolved { get; init; }
}

public sealed record InteractionOption
{
    public required string Name { get; init; }
    public string? String { get; init; }
    public long? Integer { get; init; }
    public double? Number { get; init; }
    public bool? Boolean { get; init; }
    public bool? Focused { get; init; }
    public IReadOnlyList<InteractionOption> Options { get; init; } = [];
}

public sealed record MessageComponentData
{
    public required string CustomId { get; init; }
    public required int ComponentType { get; init; }
    public string[]? Values { get; init; }
    public string? MessageId { get; init; }
    public ResolvedData? Resolved { get; init; }
}

/// <summary>
/// Contains entities Discord resolved for an interaction: entity select menus, command options, and context menu targets
/// </summary>
public sealed record ResolvedData
{
    public Dictionary<string, Entities.DiscordUser>? Users { get; init; }
    public Dictionary<string, Entities.DiscordMember>? Members { get; init; }
    public Dictionary<string, Entities.DiscordRole>? Roles { get; init; }
    public Dictionary<string, Entities.DiscordChannel>? Channels { get; init; }
    public Dictionary<string, Entities.DiscordMessage>? Messages { get; init; }
    public Dictionary<string, Entities.Attachment>? Attachments { get; init; }
}

public sealed record ModalSubmitData
{
    public required string CustomId { get; init; }
    public IReadOnlyList<TextInputValue> Inputs { get; init; } = [];

    /// <summary>Discord's data for uploaded files and chosen entities.</summary>
    public ResolvedData? Resolved { get; init; }
}

public sealed record TextInputValue
{
    public required string CustomId { get; init; }
    public string? Value { get; init; }

    /// <summary>The chosen values or uploaded file IDs, for fields that send a list.</summary>
    public IReadOnlyList<string> Values { get; init; } = [];
}

internal sealed class InteractionResponse
{
    public int type { get; set; }
    public InteractionResponseData? data { get; set; }
}

internal sealed class InteractionResponseData
{
    public string? content { get; set; }
    public Embed[]? embeds { get; set; }
    public int? flags { get; set; }
    public IComponent[]? components { get; set; }
    public AllowedMentionsPayload? allowed_mentions { get; set; }
    public PollRequest? poll { get; set; }
}
