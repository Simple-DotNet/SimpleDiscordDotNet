namespace SimpleDiscordNet.Entities;

public sealed class DiscordGuild
{
    public required ulong Id { get; init; }
    public required string Name { get; init; }

    /// <summary>Icon hash</summary>
    public string? Icon { get; internal set; }

    /// <summary>Splash hash</summary>
    public string? Splash { get; internal set; }

    /// <summary>Discovery splash hash</summary>
    public string? Discovery_Splash { get; internal set; }

    /// <summary>ID of owner</summary>
    public ulong? Owner_Id { get; internal set; }

    /// <summary>ID of AFK channel</summary>
    public ulong? Afk_Channel_Id { get; internal set; }

    /// <summary>AFK timeout in seconds</summary>
    public int? Afk_Timeout { get; internal set; }

    /// <summary>Verification level required for the guild</summary>
    public int? Verification_Level { get; internal set; }

    /// <summary>Default message notification level</summary>
    public int? Default_Message_Notifications { get; internal set; }

    /// <summary>Explicit content filter level</summary>
    public int? Explicit_Content_Filter { get; internal set; }

    /// <summary>Roles in the guild</summary>
    public DiscordRole[]? Roles { get; internal set; }

    /// <summary>Custom guild emojis</summary>
    public DiscordEmoji[]? Emojis { get; internal set; }

    /// <summary>Enabled guild features</summary>
    public string[]? Features { get; internal set; }

    /// <summary>Required MFA level for the guild</summary>
    public int? Mfa_Level { get; internal set; }

    /// <summary>System channel ID</summary>
    public ulong? System_Channel_Id { get; internal set; }

    /// <summary>System channel flags</summary>
    public int? System_Channel_Flags { get; internal set; }

    /// <summary>Rules channel ID</summary>
    public ulong? Rules_Channel_Id { get; internal set; }

    /// <summary>Maximum number of presences for the guild</summary>
    public int? Max_Presences { get; internal set; }

    /// <summary>Maximum number of members for the guild</summary>
    public int? Max_Members { get; internal set; }

    /// <summary>Vanity URL code</summary>
    public string? Vanity_Url_Code { get; internal set; }

    /// <summary>Description of the guild</summary>
    public string? Description { get; internal set; }

    /// <summary>Banner hash</summary>
    public string? Banner { get; internal set; }

    /// <summary>Premium tier (Server Boost level)</summary>
    public int? Premium_Tier { get; internal set; }

    /// <summary>Number of boosts this guild has</summary>
    public int? Premium_Subscription_Count { get; internal set; }

    /// <summary>Preferred locale of the guild</summary>
    public string? Preferred_Locale { get; internal set; }

    /// <summary>Public updates channel ID</summary>
    public ulong? Public_Updates_Channel_Id { get; internal set; }

    /// <summary>NSFW level</summary>
    public int? Nsfw_Level { get; internal set; }

    /// <summary>Whether the guild has the boost progress bar enabled</summary>
    public bool? Premium_Progress_Bar_Enabled { get; internal set; }

    /// <summary>
    /// Gets the guild's icon URL. Returns null if no icon.
    /// </summary>
    /// <param name="size">Image size (power of 2, between 16 and 4096)</param>
    /// <param name="format">Image format (png, jpg, webp, gif)</param>
    public string? GetIconUrl(int size = 256, string format = "png")
    {
        return string.IsNullOrEmpty(Icon) ? null : $"https://cdn.discordapp.com/icons/{Id}/{Icon}.{format}?size={size}";
    }

    /// <summary>
    /// Gets the guild's banner URL. Returns null if no banner.
    /// </summary>
    public string? GetBannerUrl(int size = 1024, string format = "png")
    {
        return string.IsNullOrEmpty(Banner) ? null : $"https://cdn.discordapp.com/banners/{Id}/{Banner}.{format}?size={size}";
    }

    /// <summary>
    /// Gets the guild's splash URL. Returns null if no splash.
    /// </summary>
    public string? GetSplashUrl(int size = 1024, string format = "png")
    {
        return string.IsNullOrEmpty(Splash) ? null : $"https://cdn.discordapp.com/splashes/{Id}/{Splash}.{format}?size={size}";
    }

    /// <summary>
    /// Checks if the guild has a specific feature enabled.
    /// Example features: "COMMUNITY", "VERIFIED", "PARTNERED", "ANIMATED_ICON", "BANNER", etc.
    /// </summary>
    public bool HasFeature(string feature) => Features?.Contains(feature, StringComparer.OrdinalIgnoreCase) ?? false;

    /// <summary>
    /// Returns true if this is a Community guild.
    /// </summary>
    public bool IsCommunity => HasFeature("COMMUNITY");

    /// <summary>
    /// Returns true if this guild is verified.
    /// </summary>
    public bool IsVerified => HasFeature("VERIFIED");

    /// <summary>
    /// Returns true if this guild is partnered.
    /// </summary>
    public bool IsPartnered => HasFeature("PARTNERED");

    /// <summary>
    /// Gets all members in this guild from cache.
    /// Returns an empty list if no members are cached.
    /// </summary>
    public IReadOnlyList<DiscordMember> Members => Context.DiscordContext.GetMembersInGuild(Id);

    /// <summary>
    /// Gets all channels in this guild from cache.
    /// Returns an empty list if no channels are cached.
    /// </summary>
    public IReadOnlyList<DiscordChannel> Channels => Context.DiscordContext.GetChannelsInGuild(Id);

    /// <summary>
    /// Creates a new channel in this guild.
    /// Example: await guild.CreateChannelAsync("general", ChannelType.GuildText);
    /// Example: await guild.CreateChannelAsync("general", ChannelType.GuildText, category);
    /// </summary>
    public Task<DiscordChannel?> CreateChannelAsync(string name, ChannelType type, DiscordChannel? parent = null, object[]? permissionOverwrites = null, CancellationToken ct = default)
        => Context.DiscordContext.Operations.CreateChannelAsync(Id, name, type, parent?.Id.ToString(), permissionOverwrites, ct);

    /// <summary>
    /// Creates a new category in this guild.
    /// Example: await guild.CreateCategoryAsync("General Channels");
    /// </summary>
    public Task<DiscordChannel?> CreateCategoryAsync(string name, CancellationToken ct = default)
        => Context.DiscordContext.Operations.CreateChannelAsync(Id, name, ChannelType.GuildCategory, ct: ct);
}
