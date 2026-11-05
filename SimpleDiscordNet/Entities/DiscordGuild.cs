using System.Globalization;
using System.Text.Json.Serialization;

namespace SimpleDiscordNet.Entities;

public sealed class DiscordGuild
{
    private string _name = string.Empty;

    public required ulong Id { get; init; }

    public required string Name
    {
        get => _name;
        init => _name = value;
    }

    /// <summary>Icon hash</summary>
    [JsonInclude]
    public string? Icon { get; internal set; }

    /// <summary>Splash hash</summary>
    [JsonInclude]
    public string? Splash { get; internal set; }

    /// <summary>Discovery splash hash</summary>
    [JsonInclude]
    public string? Discovery_Splash { get; internal set; }

    /// <summary>ID of owner</summary>
    [JsonInclude]
    public ulong? Owner_Id { get; internal set; }

    /// <summary>ID of AFK channel</summary>
    [JsonInclude]
    public ulong? Afk_Channel_Id { get; internal set; }

    /// <summary>AFK timeout in seconds</summary>
    [JsonInclude]
    public int? Afk_Timeout { get; internal set; }

    /// <summary>Verification level required for the guild</summary>
    [JsonInclude]
    public int? Verification_Level { get; internal set; }

    /// <summary>Default message notification level</summary>
    [JsonInclude]
    public int? Default_Message_Notifications { get; internal set; }

    /// <summary>Explicit content filter level</summary>
    [JsonInclude]
    public int? Explicit_Content_Filter { get; internal set; }

    /// <summary>Roles in the guild</summary>
    [JsonInclude]
    public DiscordRole[]? Roles { get; internal set; }

    /// <summary>Custom guild emojis</summary>
    [JsonInclude]
    public DiscordEmoji[]? Emojis { get; internal set; }

    /// <summary>Enabled guild features</summary>
    [JsonInclude]
    public string[]? Features { get; internal set; }

    /// <summary>Required MFA level for the guild</summary>
    [JsonInclude]
    public int? Mfa_Level { get; internal set; }

    /// <summary>System channel ID</summary>
    [JsonInclude]
    public ulong? System_Channel_Id { get; internal set; }

    /// <summary>System channel flags</summary>
    [JsonInclude]
    public int? System_Channel_Flags { get; internal set; }

    /// <summary>Rules channel ID</summary>
    [JsonInclude]
    public ulong? Rules_Channel_Id { get; internal set; }

    /// <summary>Maximum number of presences for the guild</summary>
    [JsonInclude]
    public int? Max_Presences { get; internal set; }

    /// <summary>Maximum number of members for the guild</summary>
    [JsonInclude]
    public int? Max_Members { get; internal set; }

    /// <summary>Total number of members in the guild</summary>
    [JsonInclude]
    public int? Member_Count { get; internal set; }

    /// <summary>Vanity URL code</summary>
    [JsonInclude]
    public string? Vanity_Url_Code { get; internal set; }

    /// <summary>Description of the guild</summary>
    [JsonInclude]
    public string? Description { get; internal set; }

    /// <summary>Banner hash</summary>
    [JsonInclude]
    public string? Banner { get; internal set; }

    /// <summary>Premium tier (Server Boost level)</summary>
    [JsonInclude]
    public int? Premium_Tier { get; internal set; }

    /// <summary>Number of boosts this guild has</summary>
    [JsonInclude]
    public int? Premium_Subscription_Count { get; internal set; }

    /// <summary>Preferred locale of the guild</summary>
    [JsonInclude]
    public string? Preferred_Locale { get; internal set; }

    /// <summary>Public updates channel ID</summary>
    [JsonInclude]
    public ulong? Public_Updates_Channel_Id { get; internal set; }

    /// <summary>NSFW level</summary>
    [JsonInclude]
    public int? Nsfw_Level { get; internal set; }

    /// <summary>Whether the guild has the boost progress bar enabled</summary>
    [JsonInclude]
    public bool? Premium_Progress_Bar_Enabled { get; internal set; }

    /// <summary>
    /// Copies fresh data from <paramref name="source"/> into this instance, so channels, members
    /// and roles that already reference this guild stay valid. Fields a payload can omit
    /// (roles, emojis, member count) are kept when the source doesn't have them.
    /// </summary>
    internal void CopyFrom(DiscordGuild source)
    {
        _name = source._name;
        Icon = source.Icon;
        Splash = source.Splash;
        Discovery_Splash = source.Discovery_Splash;
        Owner_Id = source.Owner_Id;
        Afk_Channel_Id = source.Afk_Channel_Id;
        Afk_Timeout = source.Afk_Timeout;
        Verification_Level = source.Verification_Level;
        Default_Message_Notifications = source.Default_Message_Notifications;
        Explicit_Content_Filter = source.Explicit_Content_Filter;
        Features = source.Features;
        Mfa_Level = source.Mfa_Level;
        System_Channel_Id = source.System_Channel_Id;
        System_Channel_Flags = source.System_Channel_Flags;
        Rules_Channel_Id = source.Rules_Channel_Id;
        Max_Presences = source.Max_Presences;
        Max_Members = source.Max_Members;
        Vanity_Url_Code = source.Vanity_Url_Code;
        Description = source.Description;
        Banner = source.Banner;
        Premium_Tier = source.Premium_Tier;
        Premium_Subscription_Count = source.Premium_Subscription_Count;
        Preferred_Locale = source.Preferred_Locale;
        Public_Updates_Channel_Id = source.Public_Updates_Channel_Id;
        Nsfw_Level = source.Nsfw_Level;
        Premium_Progress_Bar_Enabled = source.Premium_Progress_Bar_Enabled;

        Member_Count = source.Member_Count ?? Member_Count;
        Emojis = source.Emojis ?? Emojis;
        if (source.Roles is not null)
        {
            foreach (DiscordRole role in source.Roles)
            {
                role.Guild = this;
            }
            Roles = source.Roles;
        }
    }

    /// <summary>
    /// Gets the guild's icon URL. Returns null if no icon.
    /// </summary>
    /// <param name="size">Image size (power of 2, between 16 and 4096)</param>
    /// <param name="format">Image format (png, jpg, webp, gif)</param>
    public string? GetIconUrl(int size = 256, string format = "png")
    {
        return string.IsNullOrEmpty(Icon) ? null : $"https://cdn.discordapp.com/icons/{Id.ToString(CultureInfo.InvariantCulture)}/{Icon}.{format}?size={size.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// Gets the guild's banner URL. Returns null if no banner.
    /// </summary>
    public string? GetBannerUrl(int size = 1024, string format = "png")
    {
        return string.IsNullOrEmpty(Banner) ? null : $"https://cdn.discordapp.com/banners/{Id.ToString(CultureInfo.InvariantCulture)}/{Banner}.{format}?size={size.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// Gets the guild's splash URL. Returns null if no splash.
    /// </summary>
    public string? GetSplashUrl(int size = 1024, string format = "png")
    {
        return string.IsNullOrEmpty(Splash) ? null : $"https://cdn.discordapp.com/splashes/{Id.ToString(CultureInfo.InvariantCulture)}/{Splash}.{format}?size={size.ToString(CultureInfo.InvariantCulture)}";
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
    public Task<DiscordChannel?> CreateChannelAsync(string name, ChannelType type, DiscordChannel? parent = null, ChannelPermissionOverwrite[]? permissionOverwrites = null, CancellationToken ct = default)
        => Context.DiscordContext.Operations.CreateChannelAsync(Id, name, type, parent?.Id.ToString(CultureInfo.InvariantCulture), permissionOverwrites, ct);

    /// <summary>
    /// Creates a new category in this guild.
    /// Example: await guild.CreateCategoryAsync("General Channels");
    /// </summary>
    public Task<DiscordChannel?> CreateCategoryAsync(string name, CancellationToken ct = default)
        => Context.DiscordContext.Operations.CreateChannelAsync(Id, name, ChannelType.GuildCategory, ct: ct);

    /// <summary>
    /// Finds members whose username or nickname starts with the query.
    /// Example: var members = await guild.SearchMembersAsync("sam");
    /// </summary>
    public Task<IEnumerable<DiscordMember>> SearchMembersAsync(string query, int limit = 100, CancellationToken ct = default)
        => Context.DiscordContext.Operations.SearchMembersAsync(Id, query, limit, ct);

    /// <summary>
    /// Searches this guild's messages. Needs the Read Message History permission and the Message Content intent.
    /// Example: var results = await guild.SearchMessagesAsync("release notes");
    /// </summary>
    public Task<IEnumerable<DiscordMessage>> SearchMessagesAsync(string content, int limit = 25, CancellationToken ct = default)
        => Context.DiscordContext.Operations.SearchMessagesAsync(Id, content, limit, ct);

    /// <summary>
    /// Bans up to 200 users at once. Requires the Ban Members and Manage Guild permissions.
    /// Example: var result = await guild.BulkBanAsync(userIds, reason: "Raid");
    /// </summary>
    /// <param name="userIds">Users to ban</param>
    /// <param name="deleteMessageSeconds">How many seconds of their recent messages to delete, up to 604800 (7 days)</param>
    /// <param name="ct">Cancellation token</param>
    /// <param name="reason">Reason shown in the audit log</param>
    public Task<BulkBanResult?> BulkBanAsync(IEnumerable<ulong> userIds, int? deleteMessageSeconds = null, CancellationToken ct = default, string? reason = null)
        => Context.DiscordContext.Operations.BulkBanAsync(Id, userIds, deleteMessageSeconds, ct, reason);

    /// <summary>
    /// Gets this guild's active threads.
    /// Example: foreach (var thread in await guild.GetActiveThreadsAsync()) { }
    /// </summary>
    public Task<IEnumerable<DiscordChannel>> GetActiveThreadsAsync(CancellationToken ct = default)
        => Context.DiscordContext.Operations.GetActiveThreadsAsync(Id, ct);
}
