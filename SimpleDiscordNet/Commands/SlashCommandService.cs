using System.Runtime.InteropServices;
using SimpleDiscordNet.Logging;
using SimpleDiscordNet.Models;
using SimpleDiscordNet.Rest;

namespace SimpleDiscordNet.Commands;

internal sealed class SlashCommandService(NativeLogger logger)
{
    // New delegate-based storage populated by source generator at runtime
    private readonly Dictionary<string, CommandHandler> _ungrouped = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, CommandHandler>> _grouped = new(StringComparer.Ordinal);

    public void RegisterGenerated(string? group, string name, CommandHandler handler)
    {
        if (string.IsNullOrWhiteSpace(group))
        {
            _ungrouped[name] = handler;
        }
        else
        {
            ref Dictionary<string, CommandHandler>? dict = ref CollectionsMarshal.GetValueRefOrAddDefault(_grouped, group, out _);
            dict ??= new Dictionary<string, CommandHandler>(StringComparer.Ordinal);
            dict[name] = handler;
        }
    }

    public void RegisterGeneratedManifest(IGeneratedManifest manifest)
    {
        foreach (var kv in manifest.Ungrouped)
            _ungrouped[kv.Key] = kv.Value;
        foreach (var grp in manifest.Grouped)
        {
            if (!_grouped.TryGetValue(grp.Key, out Dictionary<string, CommandHandler>? inner))
            {
                inner = new Dictionary<string, CommandHandler>(StringComparer.Ordinal);
                _grouped[grp.Key] = inner;
            }
            foreach ((string key, CommandHandler value) in grp.Value)
                inner[key] = value;
        }
    }

    public static ApplicationCommandDefinition[] GetDefinitions(ApplicationCommandDefinition[]? fromGenerator) => fromGenerator ?? [];

    public async Task HandleAsync(InteractionCreateEvent e, RestClient rest, CancellationToken ct)
    {
        if (e.Data is not { } data)
        {
            logger.Log(LogLevel.Warning, "Interaction missing command data.");
            return;
        }

        string top = data.Name;
        string? sub = data.Subcommand;

        // Only generated delegate-based handlers are supported
        if (sub is not null)
        {
            if (_grouped.TryGetValue(top, out Dictionary<string, CommandHandler>? dict) && dict.TryGetValue(sub, out CommandHandler? handler))
            {
                await InvokeGeneratedAsync(handler, e, rest, ct, top, sub).ConfigureAwait(false);
                return;
            }
        }
        else if (_ungrouped.TryGetValue(top, out var ungrouped))
        {
            await InvokeGeneratedAsync(ungrouped, e, rest, ct, top, null).ConfigureAwait(false);
            return;
        }

        logger.Log(LogLevel.Warning, $"No generated handler found for command '{top}'{(sub is null ? string.Empty : $"/{sub}")}. Ensure the source generator is referenced and attributes are correct.");
    }

    private async Task InvokeGeneratedAsync(CommandHandler handler, InteractionCreateEvent e, RestClient rest, CancellationToken ct, string top, string? sub)
    {
        try
        {
            InteractionContext ctx = new InteractionContext(rest, e);
            if (handler.AutoDefer)
                await ctx.DeferAsync(ephemeral: false, ct).ConfigureAwait(false);
            await handler.Invoke(ctx, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, $"Error executing slash command '{top}'{(sub is null ? string.Empty : $"/{sub}")}: {ex.Message}", ex);
        }
    }

    // All reflection-based utilities removed for pure source-generator mode
}
