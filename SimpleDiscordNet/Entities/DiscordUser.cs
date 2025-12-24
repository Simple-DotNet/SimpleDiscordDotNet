namespace SimpleDiscordNet.Entities;

public sealed class DiscordUser
{
    public required ulong Id { get; init; }
    public required string Username { get; init; }

    /// <summary>User's 4-digit discriminator (deprecated by Discord, may be 0 for new usernames)</summary>
    public ushort Discriminator { get; internal set; }

    /// <summary>User's display name (new username system)</summary>
    public string? Global_Name { get; internal set; }

    /// <summary>User's avatar hash</summary>
    public string? Avatar { get; internal set; }

    /// <summary>Whether the user is a bot</summary>
    public bool? Bot { get; internal set; }

    /// <summary>Whether the user is an Official Discord System user</summary>
    public bool? System { get; internal set; }

    /// <summary>User's banner hash</summary>
    public string? Banner { get; internal set; }

    /// <summary>User's banner color (as integer)</summary>
    public int? Accent_Color { get; internal set; }

    /// <summary>User's public flags</summary>
    public int? Public_Flags { get; internal set; }

    /// <summary>
    /// All guilds the bot shares with this user. Useful for checking mutual servers.
    /// Updated when members are cached. Empty array if user not in any cached guilds.
    /// </summary>
    public DiscordGuild[] Guilds { get; internal set; } = [];

    /// <summary>
    /// Gets the user's avatar URL. Returns null if no custom avatar.
    /// </summary>
    /// <param name="size">Image size (power of 2, between 16 and 4096)</param>
    /// <param name="format">Image format (png, jpg, webp, gif). Defaults to png, use gif for animated avatars.</param>
    public string? GetAvatarUrl(int size = 256, string format = "png")
    {
        return string.IsNullOrEmpty(Avatar) ? null : $"https://cdn.discordapp.com/avatars/{Id}/{Avatar}.{format}?size={size}";
    }

    /// <summary>
    /// Gets the default avatar URL (for users without custom avatars).
    /// </summary>
    public string GetDefaultAvatarUrl()
    {
        // New system: uses (user_id >> 22) % 6
        // Old system: uses discriminator % 5
        int index = Discriminator == 0
            ? (int)((Id >> 22) % 6)
            : Discriminator % 5;
        return $"https://cdn.discordapp.com/embed/avatars/{index}.png";
    }

    /// <summary>
    /// Gets the display name to show for this user (global_name if available, otherwise username).
    /// </summary>
    public string DisplayName => Global_Name ?? Username;

    /// <summary>
    /// Gets the full username with discriminator if available (e.g., "Username#1234" or just "Username").
    /// </summary>
    public string FullUsername => Discriminator == 0
        ? Username
        : $"{Username}#{Discriminator:D4}"; // Format as 4 digits with leading zeros

    /// <summary>
    /// Returns true if this user is a bot account.
    /// </summary>
    public bool IsBot => Bot == true;

    /// <summary>
    /// Returns true if this user is the currently running bot (checks against DiscordContext.BotUser).
    /// Use this to ignore the bot's own messages/events.
    /// Example: if (user.IsCurrentBot) return; // Ignore self
    /// </summary>
    public bool IsCurrentBot => Context.DiscordContext.BotUser?.Id == Id;

    /// <summary>
    /// Sends a direct message to this user by creating a DM channel and sending a message.
    /// Example: await user.SendDMAsync("Hello!");
    /// </summary>
    public Task<DiscordMessage?> SendDMAsync(string content, EmbedBuilder? embed = null, CancellationToken ct = default)
        => Context.DiscordContext.Operations.SendDMAsync(Id.ToString(), content, embed, ct);
}
