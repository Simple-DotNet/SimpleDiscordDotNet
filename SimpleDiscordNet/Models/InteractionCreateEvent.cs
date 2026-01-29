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
}

public sealed record InteractionOption
{
    public required string Name { get; init; }
    public string? String { get; init; }
    public long? Integer { get; init; }
    public bool? Boolean { get; init; }
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
/// Contains resolved entities from entity select menus (user, role, channel, mentionable)
/// </summary>
public sealed record ResolvedData
{
    public Dictionary<string, Entities.DiscordUser>? Users { get; init; }
    public Dictionary<string, Entities.DiscordMember>? Members { get; init; }
    public Dictionary<string, Entities.DiscordRole>? Roles { get; init; }
    public Dictionary<string, Entities.DiscordChannel>? Channels { get; init; }
}

public sealed record ModalSubmitData
{
    public required string CustomId { get; init; }
    public IReadOnlyList<TextInputValue> Inputs { get; init; } = [];
}

public sealed record TextInputValue
{
    public required string CustomId { get; init; }
    public string? Value { get; init; }
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
    public object[]? components { get; set; }
}
