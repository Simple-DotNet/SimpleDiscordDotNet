namespace SimpleDiscordNet.Commands;

/// <summary>
/// Marks a class as a slash command subcommand group under a top-level command.
/// Requires <see cref="SlashCommandGroupAttribute"/> on the same class.
/// All methods in the class annotated with <see cref="SlashCommandAttribute"/> become subcommands under this subgroup.
/// Subgroup name is normalized to lowercase and must be 1-32 chars of a-z, 0-9, '-', '_'.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SlashCommandSubGroupAttribute : Attribute
{
    /// <summary>Subcommand group name.</summary>
    public string Name { get; }
    /// <summary>Human-readable subgroup description (1-100 chars). Optional.</summary>
    public string? Description { get; }

    public SlashCommandSubGroupAttribute(string name, string? description = null)
    {
        Name = name;
        Description = description;
    }
}
