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

        // Prefer generated delegate-based handler when available
        ComponentHandler? gmatch = _generated.FirstOrDefault(h => (!h.Prefix && string.Equals(h.Id, customId, StringComparison.Ordinal))
                                                               || (h.Prefix && customId.StartsWith(h.Id, StringComparison.Ordinal)));
        if (gmatch is not null)
        {
            InteractionContext? ctx = null;
            bool deferred = false;
            try
            {
                ctx = new InteractionContext(rest, e);
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
                if (ctx is not null)
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
                            await ctx.RespondAsync($"❌ An error occurred while handling this interaction.", null, true, ct).ConfigureAwait(false);
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
            await ctx.RespondAsync($"❌ Component handler not found: `{customId}`", null, true, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Failed to send error response for missing component handler: {ex.Message}", ex);
        }
    }
}
