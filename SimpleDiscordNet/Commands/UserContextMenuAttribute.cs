namespace SimpleDiscordNet.Commands;

[AttributeUsage(AttributeTargets.Method)]
public sealed class UserContextMenuAttribute : Attribute
{
    public string Name { get; }
    public UserContextMenuAttribute(string name) => Name = name;
}
