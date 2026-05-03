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

        string? focusedOption = FindFocusedOption(data.Options);
        string? focusedValue = null;

        if (focusedOption is not null)
        {
            focusedValue = FindFocusedValue(data.Options);
        }

        if (focusedOption is null)
        {
            logger.Log(LogLevel.Debug, "Could not determine focused option for autocomplete.");
            await SendEmptyChoicesAsync(e, rest, ct).ConfigureAwait(false);
            return;
        }

        string key = data.SubcommandGroup is not null && data.Subcommand is not null
            ? $"{data.Name}:{data.SubcommandGroup}:{data.Subcommand}:{focusedOption}"
            : data.SubcommandGroup is not null
                ? $"{data.Name}:{data.SubcommandGroup}:{focusedOption}"
                : data.Subcommand is not null
                    ? $"{data.Name}:{data.Subcommand}:{focusedOption}"
                    : $"{data.Name}:{focusedOption}";
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
        var payload = new AutocompleteResponsePayload(choices.ToArray());
        await rest.PostInteractionCallbackAsync(e.Id, e.Token, payload, ct).ConfigureAwait(false);
    }

    private static async Task SendEmptyChoicesAsync(InteractionCreateEvent e, RestClient rest, CancellationToken ct)
    {
        var payload = new AutocompleteResponsePayload([]);
        await rest.PostInteractionCallbackAsync(e.Id, e.Token, payload, ct).ConfigureAwait(false);
    }

    private static string? FindFocusedOption(IReadOnlyList<InteractionOption>? options)
    {
        if (options is not { Count: > 0 })
            return null;

        foreach (InteractionOption opt in options)
        {
            if (opt.Focused is true)
                return opt.Name;

            if (opt.Options is { Count: > 0 })
            {
                string? nested = FindFocusedOption(opt.Options);
                if (nested is not null)
                    return nested;
            }
        }

        return null;
    }

    private static string? FindFocusedValue(IReadOnlyList<InteractionOption>? options)
    {
        if (options is not { Count: > 0 })
            return null;

        foreach (InteractionOption opt in options)
        {
            if (opt.Focused is true)
                return opt.String;

            if (opt.Options is { Count: > 0 })
            {
                string? nested = FindFocusedValue(opt.Options);
                if (nested is not null)
                    return nested;
            }
        }

        return null;
    }
}

internal sealed class AutocompleteResponsePayload
{
    public int type { get; } = 8;
    public AutocompleteResponseData data { get; }
    public AutocompleteResponsePayload(CommandChoice[] choices) { data = new AutocompleteResponseData { choices = choices }; }
}

internal sealed class AutocompleteResponseData
{
    public CommandChoice[] choices { get; init; } = [];
}
