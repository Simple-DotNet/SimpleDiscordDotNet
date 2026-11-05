using System.Collections.Concurrent;
using System.Threading.Channels;

namespace SimpleDiscordNet.Commands;

/// <summary>
/// Routes button clicks to code waiting in ConfirmAsync, ChooseAsync or PaginateAsync.
/// Each session has a unique ID inside its buttons' custom_ids.
/// </summary>
internal static class InteractiveSessions
{
    private const string Prefix = "sdn~";
    private const char Separator = '~';

    private static readonly ConcurrentDictionary<string, Channel<InteractionContext>> Sessions = new();

    public static string Open(out ChannelReader<InteractionContext> clicks)
    {
        string id = Guid.NewGuid().ToString("N");
        Channel<InteractionContext> channel = Channel.CreateUnbounded<InteractionContext>();
        Sessions[id] = channel;
        clicks = channel.Reader;
        return id;
    }

    public static void Close(string session) => Sessions.TryRemove(session, out _);

    public static string ButtonId(string session, string action) => $"{Prefix}{session}{Separator}{action}";

    public static bool IsSessionButton(string customId) => customId.StartsWith(Prefix, StringComparison.Ordinal);

    public static string ActionOf(string customId) => customId[(customId.LastIndexOf(Separator) + 1)..];

    /// <summary>
    /// Hands a click to the waiting session.
    /// </summary>
    /// <returns>False if the session has ended.</returns>
    public static bool TryDeliver(string customId, InteractionContext click)
    {
        int start = Prefix.Length;
        int end = customId.IndexOf(Separator, start);
        if (end < 0) return false;

        return Sessions.TryGetValue(customId[start..end], out Channel<InteractionContext>? channel)
            && channel.Writer.TryWrite(click);
    }
}
