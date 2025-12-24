namespace SimpleDiscordNet.Commands;

/// <summary>
/// Marks a method as a slash command handler.
/// Use together with <see cref="SlashCommandGroupAttribute"/> on the containing class
/// to create grouped subcommands, or apply to a method in any class for a standalone command.
/// Names are normalized to lowercase and must be 1-32 chars of a-z, 0-9, '-', '_'.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class SlashCommandAttribute : Attribute
{
    /// <summary>Command name (ungrouped) or subcommand name (when class has SlashCommandGroupAttribute).</summary>
    public string Name { get; }
    /// <summary>Human-readable description (1-100 chars). Optional.</summary>
    public string? Description { get; }

    public SlashCommandAttribute(string name, string? description = null)
    {
        Name = name;
        Description = description;
    }
}
