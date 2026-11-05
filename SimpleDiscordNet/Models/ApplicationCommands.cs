using System.Globalization;
using System.Text.Json;

namespace SimpleDiscordNet.Models;

/// <summary>
/// Minimal definition model for Discord application commands used for sync (guild-scoped during development).
/// Supports both subcommands (nested options) and command parameters (type 3-11).
/// </summary>
public sealed class ApplicationCommandDefinition
{
    public required string name { get; set; }
    public int type { get; set; } // 1 = SUB_COMMAND, 2 = SUB_COMMAND_GROUP, 3 = STRING, 4 = INTEGER, 5 = BOOLEAN, 6 = USER, 7 = CHANNEL, 8 = ROLE, 9 = MENTIONABLE, 10 = NUMBER, 11 = ATTACHMENT
    public required string description { get; set; }
    public ApplicationCommandDefinition[]? options { get; set; } // For subcommands or parameters
    public bool? required { get; set; } // For command options

    // String constraints
    public int? min_length { get; set; }
    public int? max_length { get; set; }

    // Numeric constraints (INTEGER and NUMBER types)
    public double? min_value { get; set; }
    public double? max_value { get; set; }

    // Channel type restrictions (CHANNEL type only)
    public int[]? channel_types { get; set; }

    // Choices (STRING, INTEGER, NUMBER)
    public CommandChoice[]? choices { get; set; }

    // Autocomplete flag (cannot be used with choices)
    public bool? autocomplete { get; set; }

    // Permission restrictions (sets default member permissions as a bitset string)
    // Example: "2048" for KickMembers, "0" to restrict to admins only
    // Reference: https://discord.com/developers/docs/topics/permissions
    public string? default_member_permissions { get; set; }

    // Where a global command can be installed: 0 = server, 1 = user
    public int[]? integration_types { get; set; }

    // Where a global command can be used: 0 = server, 1 = bot DM, 2 = private channel
    public int[]? contexts { get; set; }

    // Translations, keyed by Discord locale
    public Dictionary<string, string>? name_localizations { get; set; }
    public Dictionary<string, string>? description_localizations { get; set; }

    /// <summary>
    /// Returns a copy for guild registration. Install and usage contexts only apply to global commands.
    /// </summary>
    internal ApplicationCommandDefinition ForGuild()
    {
        ApplicationCommandDefinition copy = (ApplicationCommandDefinition)MemberwiseClone();
        copy.integration_types = null;
        copy.contexts = null;
        return copy;
    }
}

public sealed class CommandChoice
{
    public required string name { get; set; }
    public JsonElement? value { get; set; }

    /// <summary>Creates a choice with a text value.</summary>
    public static CommandChoice Create(string name, string value)
        => new() { name = name, value = ParseValue($"\"{JsonEncodedText.Encode(value)}\"") };

    /// <summary>Creates a choice with a number value.</summary>
    public static CommandChoice Create(string name, double value)
        => new() { name = name, value = ParseValue(value.ToString("R", CultureInfo.InvariantCulture)) };

    private static JsonElement ParseValue(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
