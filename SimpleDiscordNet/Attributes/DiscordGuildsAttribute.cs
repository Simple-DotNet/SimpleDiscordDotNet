namespace SimpleDiscordNet.Attributes;

/// <summary>
/// Marks a class or method as requiring access to cached Discord guild data via DiscordContext.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class DiscordGuildsAttribute : Attribute
{
}
