namespace SimpleDiscordNet.Commands;

[AttributeUsage(AttributeTargets.Method)]
public sealed class AutocompleteAttribute : Attribute
{
    public string CommandName { get; }
    public string OptionName { get; }
    public AutocompleteAttribute(string commandName, string optionName)
    {
        CommandName = commandName;
        OptionName = optionName;
    }
}
