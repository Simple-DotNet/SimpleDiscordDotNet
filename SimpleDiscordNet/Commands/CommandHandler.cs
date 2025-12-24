namespace SimpleDiscordNet.Commands;

public sealed record CommandHandler(bool HasContext, bool AutoDefer, Func<InteractionContext, CancellationToken, ValueTask> Invoke);
