namespace SimpleDiscordNet.Commands;

/// <param name="HasContext">Whether the handler takes an InteractionContext</param>
/// <param name="AutoDefer">Whether to defer before running the handler</param>
/// <param name="DeferEphemeral">Whether the deferred reply is private</param>
/// <param name="Invoke">Runs the handler</param>
/// <param name="Checks">Checks that must pass first, from attributes such as [Cooldown]</param>
public sealed record CommandHandler(bool HasContext, bool AutoDefer, bool DeferEphemeral, Func<InteractionContext, CancellationToken, ValueTask> Invoke, IReadOnlyList<CommandCheck>? Checks = null);
