namespace SimpleDiscordNet.Commands;

/// <summary>
/// Marks a class as a slash command group. All methods in the class annotated with
/// <see cref="SlashCommandAttribute"/> become subcommands under this group.
/// Group name is normalized to lowercase and must be 1-32 chars of a-z, 0-9, '-', '_'.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SlashCommandGroupAttribute : Attribute
{
    /// <summary>Top-level group name.</summary>
    public string Name { get; }
    /// <summary>Human-readable group description (1-100 chars). Optional.</summary>
    public string? Description { get; }

    public SlashCommandGroupAttribute(string name, string? description = null)
    {
        Name = name;
        Description = description;
    }
}
