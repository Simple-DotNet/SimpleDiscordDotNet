namespace SimpleDiscordNet.Models;

/// <summary>
/// Minimal definition model for Discord application commands used for sync (guild-scoped during development).
/// Supports both subcommands (nested options) and command parameters (type 3-11).
/// </summary>
public sealed class ApplicationCommandDefinition
{
    public required string name { get; set; }
    public int type { get; set; } // 1 = SUB_COMMAND/SUB_COMMAND_GROUP, 3 = STRING, 4 = INTEGER, 5 = BOOLEAN, 6 = USER, 7 = CHANNEL, 8 = ROLE, 9 = MENTIONABLE, 10 = NUMBER, 11 = ATTACHMENT
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
}

public sealed class CommandChoice
{
    public required string name { get; set; }
    public object value { get; set; } = null!; // string, int, or double
}
