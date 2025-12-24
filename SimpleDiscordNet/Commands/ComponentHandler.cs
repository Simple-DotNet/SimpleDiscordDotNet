namespace SimpleDiscordNet.Commands;

public sealed record ComponentHandler(
    string Id,
    bool Prefix,
    bool HasContext,
    bool AutoDefer,
    Func<InteractionContext, CancellationToken, ValueTask> Invoke);
