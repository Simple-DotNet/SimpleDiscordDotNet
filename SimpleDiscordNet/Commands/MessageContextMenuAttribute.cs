namespace SimpleDiscordNet.Commands;

[AttributeUsage(AttributeTargets.Method)]
public sealed class MessageContextMenuAttribute : Attribute
{
    public string Name { get; }
    public MessageContextMenuAttribute(string name) => Name = name;
}
