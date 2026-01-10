namespace SimpleDiscordNet.Commands;

/// <summary>
/// Marks a command handler to use ephemeral (private) responses.
/// When auto-defer is enabled, the defer will use ephemeral: true.
/// All responses from this handler will only be visible to the user who triggered the interaction.
///
/// Can be applied to individual methods or entire classes.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class EphemeralAttribute : Attribute
{
}
