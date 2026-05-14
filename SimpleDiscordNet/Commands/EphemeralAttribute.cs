namespace SimpleDiscordNet.Commands;

/// <summary>
/// Marks a command handler to use ephemeral (private) responses.
/// When auto-defer is enabled, the defer will use ephemeral: true.
/// Followup messages sent via FollowupAsync/RespondAsync will default to ephemeral
/// when this attribute is present. Use ephemeral: false to override on a per-message basis.
///
/// Can be applied to individual methods or entire classes.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class EphemeralAttribute : Attribute
{
}
