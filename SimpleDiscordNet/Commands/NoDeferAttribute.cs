namespace SimpleDiscordNet.Commands;

/// <summary>
/// Disables automatic deferral for the interaction handler.
/// Apply to slash command methods, component/modal handler methods, or classes to take manual control of the interaction response.
///
/// When applied to a class (e.g., SlashCommandGroup), all methods in that class will not auto-defer.
///
/// Default behavior (without this attribute): automatic deferral is ENABLED.
/// Use [NoDefer] to disable auto-defer and manually control responses using:
/// - <see cref="InteractionContext.DeferResponseAsync(bool, System.Threading.CancellationToken)"/> for slash commands
/// - <see cref="InteractionContext.DeferUpdateAsync(System.Threading.CancellationToken)"/> for components
/// - <see cref="InteractionContext.RespondAsync(string, EmbedBuilder?, bool, System.Threading.CancellationToken)"/> for immediate responses
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class NoDeferAttribute : Attribute
{
}
