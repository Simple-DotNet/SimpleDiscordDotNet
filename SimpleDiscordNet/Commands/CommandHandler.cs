namespace SimpleDiscordNet.Commands;

public sealed record CommandHandler(bool HasContext, bool AutoDefer, bool DeferEphemeral, Func<InteractionContext, CancellationToken, ValueTask> Invoke);
