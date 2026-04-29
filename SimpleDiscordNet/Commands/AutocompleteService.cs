using SimpleDiscordNet.Logging;
using SimpleDiscordNet.Models;
using SimpleDiscordNet.Rest;

namespace SimpleDiscordNet.Commands;

internal sealed class AutocompleteService(NativeLogger logger)
{
    private readonly Dictionary<string, AutocompleteHandler> _handlers = new(StringComparer.Ordinal);

    public void RegisterGenerated(IGeneratedManifest manifest)
    {
        foreach ((string key, AutocompleteHandler handler) in manifest.AutocompleteHandlers)
            _handlers[key] = handler;
    }

    public async Task HandleAsync(InteractionCreateEvent e, RestClient rest, CancellationToken ct)
    {
        if (e.Data is not { } data || data.Name is null)
        {
            logger.Log(LogLevel.Warning, "Autocomplete interaction missing command data.");
            return;
        }

        string? focusedOption = null;
        string? focusedValue = null;

        if (data.Options is { Count: >0 })
        {
            foreach (InteractionOption opt in data.Options)
            {
                if (opt is { Name: not null } && (string.Equals(opt.Name, data.Name, StringComparison.Ordinal) || opt.String is not null))
                {
                    focusedOption = opt.Name;
                    focusedValue = opt.String;
                }
            }
        }

        if (focusedOption is null)
        {
            logger.Log(LogLevel.Debug, "Could not determine focused option for autocomplete.");
            await SendEmptyChoicesAsync(e, rest, ct).ConfigureAwait(false);
            return;
        }

        string key = $"{data.Name}:{focusedOption}";
        if (!_handlers.TryGetValue(key, out AutocompleteHandler? handler))
        {
            logger.Log(LogLevel.Debug, $"No autocomplete handler found for '{key}'");
            await SendEmptyChoicesAsync(e, rest, ct).ConfigureAwait(false);
            return;
        }

        try
        {
            InteractionContext ctx = new(rest, e);
            var choices = await handler.Invoke(ctx, ct).ConfigureAwait(false);
            await SendChoicesAsync(e, choices, rest, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, $"Error executing autocomplete handler for '{key}': {ex.Message}", ex);
            try { await SendEmptyChoicesAsync(e, rest, ct).ConfigureAwait(false); }
            catch { /* ignore send failures */ }
        }
    }

    private static async Task SendChoicesAsync(InteractionCreateEvent e, IEnumerable<CommandChoice> choices, RestClient rest, CancellationToken ct)
    {
        var payload = new { type = 8, data = new { choices = choices.ToArray() } };
        await rest.PostInteractionCallbackAsync(e.Id, e.Token, payload, ct).ConfigureAwait(false);
    }

    private static async Task SendEmptyChoicesAsync(InteractionCreateEvent e, RestClient rest, CancellationToken ct)
    {
        var payload = new { type = 8, data = new { choices = System.Array.Empty<CommandChoice>() } };
        await rest.PostInteractionCallbackAsync(e.Id, e.Token, payload, ct).ConfigureAwait(false);
    }
}
