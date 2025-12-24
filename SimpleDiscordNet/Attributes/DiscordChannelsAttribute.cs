namespace SimpleDiscordNet.Attributes;

/// <summary>
/// Marks a class or method as requiring access to cached Discord channel data via DiscordContext.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class DiscordChannelsAttribute : Attribute
{
}
