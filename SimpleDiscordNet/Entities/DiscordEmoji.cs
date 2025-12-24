namespace SimpleDiscordNet.Entities;

/// <summary>
/// Represents a Discord emoji (custom or standard Unicode emoji).
/// </summary>
public sealed record DiscordEmoji
{
    /// <summary>Emoji ID (null for standard Unicode emoji)</summary>
    public string? Id { get; init; }

    /// <summary>Emoji name (or Unicode character for standard emoji)</summary>
    public string? Name { get; init; }

    /// <summary>Roles allowed to use this emoji (for custom emoji)</summary>
    public string[]? Roles { get; init; }

    /// <summary>User that created this emoji</summary>
    public DiscordUser? User { get; init; }

    /// <summary>Whether this emoji must be wrapped in colons</summary>
    public bool? Require_Colons { get; init; }

    /// <summary>Whether this emoji is managed by an integration</summary>
    public bool? Managed { get; init; }

    /// <summary>Whether this emoji is animated</summary>
    public bool? Animated { get; init; }

    /// <summary>Whether this emoji can be used (may be false due to loss of Server Boosts)</summary>
    public bool? Available { get; init; }

    /// <summary>Returns true if this is a custom emoji (has an ID)</summary>
    public bool IsCustom => !string.IsNullOrEmpty(Id);

    /// <summary>Returns true if this is a standard Unicode emoji (no ID)</summary>
    public bool IsUnicode => string.IsNullOrEmpty(Id);

    /// <summary>
    /// Gets the reaction format for API calls.
    /// For Unicode: "👍"
    /// For custom: "emoji_name:emoji_id"
    /// </summary>
    public string GetReactionFormat()
    {
        if (IsUnicode)
            return Name ?? string.Empty;
        return $"{Name}:{Id}";
    }

    /// <summary>
    /// Creates an Emoji from a Unicode character.
    /// Example: Emoji.Unicode("👍")
    /// </summary>
    public static DiscordEmoji Unicode(string unicodeCharacter) => new() { Name = unicodeCharacter };

    /// <summary>
    /// Creates an Emoji from a custom emoji ID and name.
    /// Example: Emoji.Custom("custom_emoji", "123456789")
    /// </summary>
    public static DiscordEmoji Custom(string name, string id, bool animated = false)
        => new() { Id = id, Name = name, Animated = animated };
}
