using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Extensions;

/// <summary>
/// Extension methods for easy messaging via Channels.
/// </summary>
public static class MessagingExtensions
{
    /// <summary>
    /// Sends a message to this channel.
    /// Requires access to the bot instance.
    /// Example: await channel.SendMessageAsync(bot, "Hello!");
    /// </summary>
    public static Task SendMessageAsync(this DiscordChannel channel, IDiscordBot bot, string content, CancellationToken ct = default)
    {
        return bot.SendMessageAsync(channel.Id.ToString(), content, null, ct);
    }
}
