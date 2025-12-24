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
            try
            {
                var ctx = new InteractionContext(rest, e);
                if (e.Type == InteractionType.MessageComponent && gmatch.AutoDefer)
                    await ctx.DeferUpdateAsync(ct).ConfigureAwait(false);
                await gmatch.Invoke(ctx, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, $"Error executing component handler for '{customId}': {ex.Message}", ex);
            }
            return;
        }

        _logger.Log(LogLevel.Debug, $"No generated component handler found for custom_id '{customId}'. Ensure the source generator is referenced and attributes are correct.");
    }
}
