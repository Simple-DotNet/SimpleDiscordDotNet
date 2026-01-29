using System.Runtime.InteropServices;
using SimpleDiscordNet.Logging;
using SimpleDiscordNet.Models;
using SimpleDiscordNet.Rest;

namespace SimpleDiscordNet.Commands;

internal sealed class SlashCommandService(NativeLogger logger, CommandPermissionService? permissionService = null)
{
    // New delegate-based storage populated by source generator at runtime
    private readonly Dictionary<string, CommandHandler> _ungrouped = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, CommandHandler>> _grouped = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, Dictionary<string, CommandHandler>>> _subGrouped = new(StringComparer.Ordinal);
    private readonly CommandPermissionService? _permissionService = permissionService;

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

    public void RegisterGenerated(string? group, string? subGroup, string name, CommandHandler handler)
    {
        if (string.IsNullOrWhiteSpace(subGroup))
        {
            RegisterGenerated(group, name, handler);
            return;
        }

        if (string.IsNullOrWhiteSpace(group))
        {
            _ungrouped[name] = handler;
            return;
        }

        ref Dictionary<string, Dictionary<string, CommandHandler>>? groupDict = ref CollectionsMarshal.GetValueRefOrAddDefault(_subGrouped, group, out _);
        groupDict ??= new Dictionary<string, Dictionary<string, CommandHandler>>(StringComparer.Ordinal);
        if (!groupDict.TryGetValue(subGroup, out Dictionary<string, CommandHandler>? subDict))
        {
            subDict = new Dictionary<string, CommandHandler>(StringComparer.Ordinal);
            groupDict[subGroup] = subDict;
        }
        subDict[name] = handler;
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
        foreach (var grp in manifest.SubGrouped)
        {
            if (!_subGrouped.TryGetValue(grp.Key, out Dictionary<string, Dictionary<string, CommandHandler>>? groupDict))
            {
                groupDict = new Dictionary<string, Dictionary<string, CommandHandler>>(StringComparer.Ordinal);
                _subGrouped[grp.Key] = groupDict;
            }

            foreach (var subGroup in grp.Value)
            {
                if (!groupDict.TryGetValue(subGroup.Key, out Dictionary<string, CommandHandler>? subDict))
                {
                    subDict = new Dictionary<string, CommandHandler>(StringComparer.Ordinal);
                    groupDict[subGroup.Key] = subDict;
                }

                foreach ((string key, CommandHandler value) in subGroup.Value)
                    subDict[key] = value;
            }
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
        string? group = data.SubcommandGroup;
        string? sub = data.Subcommand;

        // Only generated delegate-based handlers are supported
        if (group is not null && sub is not null)
        {
            if (_subGrouped.TryGetValue(top, out Dictionary<string, Dictionary<string, CommandHandler>>? groupDict) &&
                groupDict.TryGetValue(group, out Dictionary<string, CommandHandler>? subDict) &&
                subDict.TryGetValue(sub, out CommandHandler? handler))
            {
                await InvokeGeneratedAsync(handler, e, rest, ct, top, sub, group).ConfigureAwait(false);
                return;
            }
        }
        else if (sub is not null)
        {
            if (_grouped.TryGetValue(top, out Dictionary<string, CommandHandler>? dict) && dict.TryGetValue(sub, out CommandHandler? handler))
            {
                await InvokeGeneratedAsync(handler, e, rest, ct, top, sub, null).ConfigureAwait(false);
                return;
            }
        }
        else if (_ungrouped.TryGetValue(top, out var ungrouped))
        {
            await InvokeGeneratedAsync(ungrouped, e, rest, ct, top, null, null).ConfigureAwait(false);
            return;
        }

        string path = group is null
            ? (sub is null ? top : $"{top}/{sub}")
            : $"{top}/{group}/{sub}";
        logger.Log(LogLevel.Warning, $"No generated handler found for command '{path}'. Ensure the source generator is referenced and attributes are correct.");

        // Send error response to Discord so it doesn't timeout
        try
        {
            InteractionContext ctx = new InteractionContext(rest, e);
            await ctx.RespondAsync($"❌ Command handler not found: `{path}`", null, true, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, $"Failed to send error response for missing handler: {ex.Message}", ex);
        }
    }

    private async Task InvokeGeneratedAsync(CommandHandler handler, InteractionCreateEvent e, RestClient rest, CancellationToken ct, string top, string? sub, string? group)
    {
        InteractionContext? ctx = null;
        bool deferred = false;
        try
        {
            ctx = new InteractionContext(rest, e);

            // Check custom runtime permissions before execution
            if (_permissionService is not null)
            {
                bool hasPermission = _permissionService.CheckPermission(e.GuildId, top, ctx);
                if (!hasPermission)
                {
                    logger.Log(LogLevel.Debug, $"User {e.Member?.User?.Id ?? e.Author?.Id} denied permission for command '{top}' in guild {e.GuildId}");
                    await ctx.RespondAsync("❌ You don't have permission to use this command in this server.", null, true, ct).ConfigureAwait(false);
                    return;
                }
            }

            if (handler.AutoDefer)
            {
                await ctx.DeferAsync(ephemeral: handler.DeferEphemeral, ct).ConfigureAwait(false);
                deferred = true;
            }
            await handler.Invoke(ctx, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            string path = group is null
                ? (sub is null ? top : $"{top}/{sub}")
                : $"{top}/{group}/{sub}";
            logger.Log(LogLevel.Error, $"Error executing slash command '{path}': {ex.Message}", ex);

            // Send error response to Discord so user knows something went wrong
            if (ctx is not null)
            {
                try
                {
                    if (deferred)
                    {
                        // If already deferred, send followup
                        await ctx.FollowupAsync($"❌ An error occurred while executing the command.", null, true, ct).ConfigureAwait(false);
                    }
                    else
                    {
                        // Send immediate error response
                        await ctx.RespondAsync($"❌ An error occurred while executing the command.", null, true, ct).ConfigureAwait(false);
                    }
                }
                catch (Exception responseEx)
                {
                    logger.Log(LogLevel.Error, $"Failed to send error response to user: {responseEx.Message}", responseEx);
                }
            }
        }
    }

    // All reflection-based utilities removed for pure source-generator mode
}
