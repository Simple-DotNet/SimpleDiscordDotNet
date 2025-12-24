namespace SimpleDiscordNet;

/// <summary>
/// Provides pre-configured embed templates for common use cases.
/// Makes it easy for beginners to create good-looking embeds quickly.
/// </summary>
public static class EmbedPresets
{
    /// <summary>
    /// Creates a success embed with green color.
    /// Example: var embed = EmbedPresets.Success("Operation completed successfully!");
    /// </summary>
    public static EmbedBuilder Success(string message, string? title = null)
    {
        EmbedBuilder builder = new EmbedBuilder()
            .WithDescription(message)
            .WithColor(DiscordColor.Green);

        if (title is not null)
            builder.WithTitle($"✅ {title}");

        return builder;
    }

    /// <summary>
    /// Creates an error embed with red color.
    /// Example: var embed = EmbedPresets.Error("Something went wrong!");
    /// </summary>
    public static EmbedBuilder Error(string message, string? title = null)
    {
        EmbedBuilder builder = new EmbedBuilder()
            .WithDescription(message)
            .WithColor(DiscordColor.Red);

        if (title is not null)
            builder.WithTitle($"❌ {title}");

        return builder;
    }

    /// <summary>
    /// Creates a warning embed with yellow/orange color.
    /// Example: var embed = EmbedPresets.Warning("Please be careful!");
    /// </summary>
    public static EmbedBuilder Warning(string message, string? title = null)
    {
        EmbedBuilder builder = new EmbedBuilder()
            .WithDescription(message)
            .WithColor(DiscordColor.Yellow);

        if (title is not null)
            builder.WithTitle($"⚠️ {title}");

        return builder;
    }

    /// <summary>
    /// Creates an info embed with blue color.
    /// Example: var embed = EmbedPresets.Info("Here's some helpful information");
    /// </summary>
    public static EmbedBuilder Info(string message, string? title = null)
    {
        EmbedBuilder builder = new EmbedBuilder()
            .WithDescription(message)
            .WithColor(DiscordColor.Blue);

        if (title is not null)
            builder.WithTitle($"ℹ️ {title}");

        return builder;
    }

    /// <summary>
    /// Creates a loading/pending embed with Discord's blurple color.
    /// Example: var embed = EmbedPresets.Loading("Processing your request...");
    /// </summary>
    public static EmbedBuilder Loading(string message = "Loading...")
    {
        return new EmbedBuilder()
            .WithDescription($"⏳ {message}")
            .WithColor(DiscordColor.Blurple);
    }

    /// <summary>
    /// Creates a help embed with purple color and formatted fields.
    /// Example: var embed = EmbedPresets.Help("My Bot", "Use /help to get started");
    /// </summary>
    public static EmbedBuilder Help(string title, string description)
    {
        return new EmbedBuilder()
            .WithTitle($"📚 {title}")
            .WithDescription(description)
            .WithColor(DiscordColor.Purple)
            .WithTimestamp(DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Creates a user profile embed with teal color.
    /// Example: var embed = EmbedPresets.UserProfile(user.Username, user.GetAvatarUrl());
    /// </summary>
    public static EmbedBuilder UserProfile(string username, string? avatarUrl = null)
    {
        EmbedBuilder builder = new EmbedBuilder()
            .WithTitle($"👤 {username}")
            .WithColor(DiscordColor.Teal);

        if (avatarUrl is not null)
            builder.WithThumbnail(avatarUrl);

        return builder;
    }

    /// <summary>
    /// Creates a server/guild info embed with Discord green.
    /// Example: var embed = EmbedPresets.ServerInfo(guild.Name, guild.GetIconUrl());
    /// </summary>
    public static EmbedBuilder ServerInfo(string serverName, string? iconUrl = null)
    {
        EmbedBuilder builder = new EmbedBuilder()
            .WithTitle($"🏰 {serverName}")
            .WithColor(DiscordColor.DiscordGreen);

        if (iconUrl is not null)
            builder.WithThumbnail(iconUrl);

        return builder;
    }

    /// <summary>
    /// Creates an announcement embed with fuchsia color.
    /// Example: var embed = EmbedPresets.Announcement("New Feature Released!", "Check out our latest update");
    /// </summary>
    public static EmbedBuilder Announcement(string title, string description)
    {
        return new EmbedBuilder()
            .WithTitle($"📢 {title}")
            .WithDescription(description)
            .WithColor(DiscordColor.Fuchsia)
            .WithTimestamp(DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Creates a simple list embed for displaying items.
    /// Example: var embed = EmbedPresets.List("Top 5 Users", "1. User1\n2. User2\n3. User3");
    /// </summary>
    public static EmbedBuilder List(string title, string items)
    {
        return new EmbedBuilder()
            .WithTitle(title)
            .WithDescription(items)
            .WithColor(DiscordColor.Blurple);
    }

    /// <summary>
    /// Creates a quote/message embed with grey color.
    /// Example: var embed = EmbedPresets.Quote("Amazing!", "User123");
    /// </summary>
    public static EmbedBuilder Quote(string text, string author)
    {
        return new EmbedBuilder()
            .WithDescription($"💬 \"{text}\"")
            .WithFooter($"— {author}")
            .WithColor(DiscordColor.Grey);
    }
}
