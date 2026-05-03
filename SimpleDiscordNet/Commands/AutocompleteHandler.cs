using SimpleDiscordNet.Models;

namespace SimpleDiscordNet.Commands;

public sealed record AutocompleteHandler(Func<InteractionContext, CancellationToken, ValueTask<IEnumerable<CommandChoice>>> Invoke);
