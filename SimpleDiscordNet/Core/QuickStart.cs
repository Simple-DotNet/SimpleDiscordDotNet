namespace SimpleDiscordNet;

/// <summary>
/// Quick start helpers for beginners to create a Discord bot with minimal code.
/// </summary>
public static class QuickStart
{
    /// <summary>
    /// Creates a simple Discord bot with commonly used settings for beginners.
    /// Includes: Guild messages, message content, direct messages.
    /// Example: var bot = QuickStart.CreateBot("your_token_here");
    /// </summary>
    /// <param name="token">Your Discord bot token from the Discord Developer Portal</param>
    /// <param name="developmentGuildId">Optional: Guild ID for testing commands (faster sync)</param>
    public static DiscordBot CreateBot(string token, string? developmentGuildId = null)
    {
        DiscordBot.Builder builder = DiscordBot.NewBuilder()
            .WithToken(token)
            .WithIntents(DiscordIntents.Guilds | DiscordIntents.GuildMessages | DiscordIntents.MessageContent | DiscordIntents.DirectMessages);

        if (developmentGuildId is not null)
        {
            builder.WithDevelopmentMode(true)
                   .WithDevelopmentGuild(developmentGuildId);
        }

        return builder.Build();
    }

    /// <summary>
    /// Creates a Discord bot with all commonly needed intents enabled.
    /// Includes: Guilds, messages, reactions, members (privileged), message content (privileged).
    /// Note: You must enable "Message Content Intent" and "Server Members Intent" in the Developer Portal.
    /// Example: var bot = QuickStart.CreateFullBot("your_token_here");
    /// </summary>
    public static DiscordBot CreateFullBot(string token, string? developmentGuildId = null)
    {
        DiscordBot.Builder builder = DiscordBot.NewBuilder()
            .WithToken(token)
            .WithIntents(
                DiscordIntents.Guilds |
                DiscordIntents.GuildMessages |
                DiscordIntents.GuildMessageReactions |
                DiscordIntents.DirectMessages |
                DiscordIntents.MessageContent |
                DiscordIntents.GuildMembers
            );

        if (developmentGuildId is not null)
        {
            builder.WithDevelopmentMode()
                   .WithDevelopmentGuild(developmentGuildId);
        }

        return builder.Build();
    }

    /// <summary>
    /// Creates a simple message-only bot (no slash commands).
    /// Example: var bot = QuickStart.CreateMessageBot("your_token_here");
    /// </summary>
    public static DiscordBot CreateMessageBot(string token)
    {
        return DiscordBot.NewBuilder()
            .WithToken(token)
            .WithIntents(DiscordIntents.Guilds | DiscordIntents.GuildMessages | DiscordIntents.MessageContent)
            .Build();
    }
}
