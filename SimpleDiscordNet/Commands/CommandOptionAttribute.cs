namespace SimpleDiscordNet.Commands;

/// <summary>
/// Marks a slash command parameter as a Discord command option.
/// The source generator will automatically extract these values from the interaction
/// and pass them to your method, with full AOT compatibility.
///
/// Supported types:
/// - string: Discord STRING (supports MinLength/MaxLength/Choices/Autocomplete)
/// - long/int: Discord INTEGER (supports MinValue/MaxValue/Choices)
/// - double/float: Discord NUMBER (supports MinValue/MaxValue/Choices)
/// - bool: Discord BOOLEAN
/// - User: Discord USER (select a user, resolves from cache)
/// - Channel: Discord CHANNEL (select a channel, supports ChannelTypes filtering, resolves from cache)
/// - Role: Discord ROLE (select a role, resolves from cache)
///
/// Examples:
/// [CommandOption("name", "Your name", MinLength = 2, MaxLength = 32)]
/// string name,
///
/// [CommandOption("age", "Your age", MinValue = 13, MaxValue = 120)]
/// int age,
///
/// [CommandOption("size", "Choose size", Choices = "Small:1,Medium:5,Large:10")]
/// int size,
///
/// [CommandOption("channel", "Select a text channel", ChannelTypes = "0")]
/// Channel channel,
///
/// [CommandOption("query", "Search query", Autocomplete = true)]
/// string query
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class CommandOptionAttribute : Attribute
{
    /// <summary>Option name as it appears in Discord (lowercase, 1-32 chars).</summary>
    public string Name { get; }

    /// <summary>Human-readable description (1-100 chars).</summary>
    public string Description { get; }

    /// <summary>Whether this option is required. Automatically inferred from nullable types if not set.</summary>
    public bool? Required { get; set; }

    // String constraints
    /// <summary>Minimum length for string options (1-6000).</summary>
    public int? MinLength { get; set; }

    /// <summary>Maximum length for string options (1-6000).</summary>
    public int? MaxLength { get; set; }

    // Numeric constraints
    /// <summary>Minimum value for integer/number options.</summary>
    public double? MinValue { get; set; }

    /// <summary>Maximum value for integer/number options.</summary>
    public double? MaxValue { get; set; }

    // Channel constraints
    /// <summary>
    /// Restrict channel selection to specific types (comma-separated).
    /// Example: "0,2" for GUILD_TEXT and GUILD_VOICE.
    /// See Discord API docs for channel type values.
    /// </summary>
    public string? ChannelTypes { get; set; }

    // Choices (for STRING, INTEGER, NUMBER)
    /// <summary>
    /// Predefined choices for this option (dropdown). Format: "Display Name:value,Display Name 2:value2"
    /// Example for strings: "Red:red,Green:green,Blue:blue"
    /// Example for numbers: "Small:1,Medium:5,Large:10"
    /// Limited to 25 choices maximum.
    /// </summary>
    public string? Choices { get; set; }

    /// <summary>
    /// Enable autocomplete for this option. Your bot must handle autocomplete interactions.
    /// Cannot be used with Choices.
    /// </summary>
    public bool Autocomplete { get; set; }

    public CommandOptionAttribute(string name, string description)
    {
        Name = name;
        Description = description;
    }
}
