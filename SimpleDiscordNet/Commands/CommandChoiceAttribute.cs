namespace SimpleDiscordNet.Commands;

/// <summary>
/// Defines a predefined choice for a command option parameter.
/// Use multiple [CommandChoice] attributes on a parameter to define dropdown options.
/// Limited to 25 choices maximum per option.
///
/// Example:
/// [CommandOption("style", "Choose a style")]
/// [CommandChoice("Formal", "formal")]
/// [CommandChoice("Casual", "casual")]
/// [CommandChoice("Enthusiastic", "enthusiastic")]
/// string style
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = true)]
public sealed class CommandChoiceAttribute : Attribute
{
    /// <summary>Display name shown to the user (1-100 chars).</summary>
    public string Name { get; }

    /// <summary>The actual value sent to your command (string, int, or double).</summary>
    public object Value { get; }

    /// <summary>
    /// Define a choice for a command option.
    /// </summary>
    /// <param name="name">Display name shown in Discord</param>
    /// <param name="value">Actual value (must match parameter type: string, int, long, or double)</param>
    public CommandChoiceAttribute(string name, object value)
    {
        Name = name;
        Value = value;
    }
}
