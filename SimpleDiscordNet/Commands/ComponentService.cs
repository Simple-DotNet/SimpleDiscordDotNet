using SimpleDiscordNet.Logging;
using SimpleDiscordNet.Models;
using SimpleDiscordNet.Rest;

namespace SimpleDiscordNet.Commands;

internal sealed class ComponentService
{
    private readonly NativeLogger _logger;
    // Delegate-based handlers populated by generator
    private readonly List<ComponentHandler> _generated = new();

    public ComponentService(NativeLogger logger)
    {
        _logger = logger;
    }

    public void RegisterGenerated(ComponentHandler handler)
        => _generated.Add(handler);

    public async Task HandleAsync(InteractionCreateEvent e, RestClient rest, CancellationToken ct)
    {
        // Determine custom_id based on interaction type
        string? customId = e.Type switch
        {
            InteractionType.MessageComponent => e.Component?.CustomId,
            InteractionType.ModalSubmit => e.Modal?.CustomId,
            _ => null
        };
        if (string.IsNullOrEmpty(customId))
            return;

        // Buttons from ConfirmAsync, ChooseAsync and PaginateAsync go to the code waiting for them
        if (InteractiveSessions.IsSessionButton(customId))
        {
            InteractionContext click = new(rest, e);
            if (!InteractiveSessions.TryDeliver(customId, click))
            {
                await click.RespondAsync("⌛ This has expired.", null, true, ct: ct).ConfigureAwait(false);
            }
            return;
        }

        // Prefer generated delegate-based handler when available
        // An exact ID wins. Otherwise the match with the most fixed text wins, to avoid ambiguity
        ComponentHandler? gmatch = null;
        Dictionary<string, string>? idValues = null;
        int bestLength = -1;
        foreach (ComponentHandler h in _generated)
        {
            if (h.Template is { } template)
            {
                if (template.LiteralLength > bestLength && template.Match(customId) is { } values)
                {
                    gmatch = h;
                    idValues = values;
                    bestLength = template.LiteralLength;
                }
                continue;
            }

            if (!h.Prefix && string.Equals(h.Id, customId, StringComparison.Ordinal))
            {
                gmatch = h;
                idValues = null;
                break;
            }
            if (h.Prefix && customId.StartsWith(h.Id, StringComparison.Ordinal) && h.Id.Length > bestLength)
            {
                gmatch = h;
                idValues = null;
                bestLength = h.Id.Length;
            }
        }
        if (gmatch is not null)
        {
            InteractionContext? ctx = null;
            bool deferred = false;
            try
            {
                ctx = new InteractionContext(rest, e) { IdValues = idValues };

                if (!await CommandChecks.PassAsync(gmatch.Checks, ctx, ct).ConfigureAwait(false))
                    return;

                if (e.Type == InteractionType.MessageComponent && gmatch.AutoDefer)
                {
                    // If ephemeral, use DeferAsync (creates new response), otherwise DeferUpdateAsync (updates message)
                    if (gmatch.DeferEphemeral)
                    {
                        await ctx.DeferAsync(ephemeral: true, ct).ConfigureAwait(false);
                    }
                    else
                    {
                        await ctx.DeferUpdateAsync(ct).ConfigureAwait(false);
                    }
                    deferred = true;
                }
                await gmatch.Invoke(ctx, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, $"Error executing component handler for '{customId}': {ex.Message}", ex);

                // Send error response to Discord so user knows something went wrong
                if (ctx is not null && !await CommandErrorHandlers.TryHandleAsync(ctx, ex, _logger).ConfigureAwait(false))
                {
                    try
                    {
                        if (deferred)
                        {
                            // If already deferred, send followup
                            await ctx.FollowupAsync($"❌ An error occurred while handling this interaction.", null, true, ct).ConfigureAwait(false);
                        }
                        else
                        {
                            // Send immediate error response
                            await ctx.RespondAsync($"❌ An error occurred while handling this interaction.", null, true, ct: ct).ConfigureAwait(false);
                        }
                    }
                    catch (Exception responseEx)
                    {
                        _logger.Log(LogLevel.Error, $"Failed to send error response to user: {responseEx.Message}", responseEx);
                    }
                }
            }
            return;
        }

        _logger.Log(LogLevel.Debug, $"No generated component handler found for custom_id '{customId}'. Ensure the source generator is referenced and attributes are correct.");

        // Send error response to Discord so it doesn't timeout
        try
        {
            InteractionContext ctx = new InteractionContext(rest, e);
            await ctx.RespondAsync($"❌ Component handler not found: `{customId}`", null, true, ct: ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Failed to send error response for missing component handler: {ex.Message}", ex);
        }
    }
}
