using SimpleDiscordNet.Models;

namespace SimpleDiscordNet.Commands;

public sealed record AutocompleteHandler
{
    public required Func<InteractionContext, CancellationToken, ValueTask<IEnumerable<CommandChoice>>> Invoke { get; init; }
}
