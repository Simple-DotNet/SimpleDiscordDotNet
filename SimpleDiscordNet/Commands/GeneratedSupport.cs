using System.Text;
using SimpleDiscordNet.Events;
using SimpleDiscordNet.Logging;
using SimpleDiscordNet.Primitives;

namespace SimpleDiscordNet.Commands;

/// <summary>
/// Supplies constructor arguments to handler classes. Set up with DiscordBot.Builder.WithServices.
/// </summary>
public static class HandlerServices
{
    private static volatile IServiceProvider? s_provider;
    private static volatile IDiscordBot? s_bot;

    internal static void Configure(IServiceProvider? provider, IDiscordBot bot)
    {
        s_provider = provider ?? s_provider;
        s_bot = bot;
    }

    /// <summary>
    /// Gets a service for a handler's constructor. The bot itself is always available.
    /// </summary>
    public static T Get<T>() where T : class
    {
        if (s_provider?.GetService(typeof(T)) is T service) return service;
        if (s_bot is T bot) return bot;

        throw new InvalidOperationException($"A handler needs {typeof(T).Name}, but it isn't available. Register it in your services and pass them to DiscordBot.Builder.WithServices.");
    }
}

/// <summary>
/// Runs [OnMemberJoined]-style event handlers. Generated code calls this.
/// </summary>
public static class GeneratedEvents
{
    private static readonly Lock Gate = new();
    private static readonly List<Action> Subscriptions = [];

    /// <summary>
    /// Connects an attribute-based handler to its event.
    /// </summary>
    public static void Subscribe(Action subscribe)
    {
        lock (Gate) Subscriptions.Add(subscribe);
        subscribe();
    }

    /// <summary>
    /// Reconnects attribute-based handlers after DiscordEvents.UnsubscribeAll. They're part of
    /// the app rather than temporary subscribers, and nothing else would reconnect them.
    /// </summary>
    internal static void Restore()
    {
        lock (Gate)
        {
            foreach (Action subscribe in Subscriptions) subscribe();
        }
    }

    /// <summary>
    /// Starts a handler off the gateway's receive loop, so a slow one can't stall the connection.
    /// Exceptions are raised through DiscordEvents.Error.
    /// </summary>
    public static void Run(Func<ValueTask> handler) => _ = RunCoreAsync(handler);

    private static async Task RunCoreAsync(Func<ValueTask> handler)
    {
        await Task.Yield();
        try
        {
            await handler().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            DiscordEvents.RaiseError(null, ex);
        }
    }
}

/// <summary>
/// Holds the method marked [OnCommandError]. Generated code registers it.
/// </summary>
public static class CommandErrorHandlers
{
    private static volatile Func<InteractionContext, Exception, ValueTask>? s_handler;

    public static void Register(Func<InteractionContext, Exception, ValueTask> handler) => s_handler = handler;

    /// <summary>
    /// Gives the error to the app's handler.
    /// </summary>
    /// <returns>False if there's no handler or it failed, so the default message should be sent.</returns>
    internal static async ValueTask<bool> TryHandleAsync(InteractionContext ctx, Exception error, NativeLogger logger)
    {
        if (s_handler is not { } handler) return false;

        try
        {
            await handler(ctx, error).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, $"[OnCommandError] handler failed: {ex.Message}", ex);
            return false;
        }
    }

    internal static void ClearForTests() => s_handler = null;
}

/// <summary>
/// Builds the reply for the command added by [assembly: GenerateHelp].
/// </summary>
public static class HelpFormatter
{
    private const int MaxPageLength = 3800;

    public static Task SendAsync(InteractionContext ctx, IReadOnlyDictionary<string, string> commands, string title, CancellationToken ct)
        => ctx.PaginateAsync(BuildPages(commands, title), ct: ct);

    internal static List<EmbedBuilder> BuildPages(IReadOnlyDictionary<string, string> commands, string title)
    {
        List<EmbedBuilder> pages = [];
        StringBuilder page = new();

        foreach (KeyValuePair<string, string> command in commands.OrderBy(static c => c.Key, StringComparer.Ordinal))
        {
            string line = $"`{command.Key}` - {command.Value}\n";
            if (page.Length + line.Length > MaxPageLength)
            {
                pages.Add(new EmbedBuilder().WithTitle(title).WithDescription(page.ToString()));
                page.Clear();
            }
            page.Append(line);
        }

        pages.Add(new EmbedBuilder().WithTitle(title).WithDescription(page.Length > 0 ? page.ToString() : "No commands yet."));
        return pages;
    }
}
