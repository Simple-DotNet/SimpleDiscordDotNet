using SimpleDiscordNet.Collections;
using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Context;

/// <summary>
/// Ambient access to cached Discord data for the currently running bot instance.
/// Consumers can read Guilds/Channels/Members/Users anywhere in their application.
/// Supports both snapshot-based access (backward compatible) and live observable collections for UI binding.
/// </summary>
public static class DiscordContext
{
    private static IDiscordContextOperations? s_operations;
    private static DiscordUser? s_botUser;
    private static Func<IReadOnlyList<DiscordGuild>>? s_guildsProvider;
    private static Func<IReadOnlyList<DiscordChannel>>? s_channelsProvider;
    private static Func<IReadOnlyList<DiscordMember>>? s_membersProvider;
    private static Func<IReadOnlyList<DiscordUser>>? s_usersProvider;
    private static Func<IReadOnlyList<DiscordRole>>? s_rolesProvider;

    // Live observable collections
    private static ObservableConcurrentDictionary<ulong, DiscordGuild>? s_liveGuilds;
    private static Func<IEnumerable<DiscordChannel>>? s_liveChannelsProvider;
    private static Func<IEnumerable<DiscordMember>>? s_liveMembersProvider;
    private static Func<ulong, ObservableConcurrentList<DiscordChannel>?>? s_liveChannelsForGuildProvider;
    private static Func<ulong, ObservableConcurrentList<DiscordMember>?>? s_liveMembersForGuildProvider;

    private static readonly DiscordGuild[] s_emptyGuilds = [];
    private static readonly DiscordChannel[] s_emptyChannels = [];
    private static readonly DiscordMember[] s_emptyMembers = [];
    private static readonly DiscordUser[] s_emptyUsers = [];
    private static readonly DiscordRole[] s_emptyRoles = [];

    /// <summary>
    /// Safe bot operations for messaging and read-only access.
    /// Does not expose lifecycle or configuration methods.
    /// </summary>
    public static IDiscordContextOperations Operations => s_operations ?? throw new InvalidOperationException("DiscordContext has not been initialized. Ensure DiscordBot has been started.");

    /// <summary>
    /// The bot's own user object. Use this to check if a user/member is the bot itself.
    /// Example: if (user.Id == DiscordContext.BotUser?.Id) return; // Ignore bot's own messages
    /// </summary>
    public static DiscordUser? BotUser => s_botUser;

    /// <summary>All guilds known to the bot</summary>
    public static IReadOnlyList<DiscordGuild> Guilds => s_guildsProvider?.Invoke() ?? s_emptyGuilds;

    /// <summary>All channels known to the bot (each channel has guild context via the Guild property)</summary>
    public static IReadOnlyList<DiscordChannel> Channels => s_channelsProvider?.Invoke() ?? s_emptyChannels;

    /// <summary>All members known to the bot (each member has guild context via the Guild property)</summary>
    public static IReadOnlyList<DiscordMember> Members => s_membersProvider?.Invoke() ?? s_emptyMembers;

    /// <summary>All distinct users known to the bot (each user has Guilds[] property with all mutual guilds)</summary>
    public static IReadOnlyList<DiscordUser> Users => s_usersProvider?.Invoke() ?? s_emptyUsers;

    /// <summary>All roles known to the bot (each role has guild context via the Guild property)</summary>
    public static IReadOnlyList<DiscordRole> Roles => s_rolesProvider?.Invoke() ?? s_emptyRoles;

    // --- Live Observable Collections for UI Binding ---

    /// <summary>
    /// Gets the live observable dictionary of guilds. Changes to this collection automatically
    /// raise INotifyCollectionChanged events, making it suitable for UI binding.
    /// Example: myListView.ItemsSource = DiscordContext.LiveGuilds.Values;
    /// </summary>
    public static ObservableConcurrentDictionary<ulong, DiscordGuild>? LiveGuilds => s_liveGuilds;

    /// <summary>
    /// Gets all live channels flattened into a single enumerable. For UI binding to a specific guild's channels,
    /// use GetLiveChannelsForGuild(guildId) instead for better performance and automatic updates.
    /// </summary>
    public static IEnumerable<DiscordChannel> LiveChannels => s_liveChannelsProvider?.Invoke() ?? Enumerable.Empty<DiscordChannel>();

    /// <summary>
    /// Gets all live members flattened into a single enumerable. For UI binding to a specific guild's members,
    /// use GetLiveMembersForGuild(guildId) instead for better performance and automatic updates.
    /// </summary>
    public static IEnumerable<DiscordMember> LiveMembers => s_liveMembersProvider?.Invoke() ?? Enumerable.Empty<DiscordMember>();

    /// <summary>
    /// Gets the live observable list of channels for a specific guild. This list automatically
    /// updates when channels are added, removed, or modified, raising INotifyCollectionChanged events.
    /// Returns null if the guild has no cached channels.
    /// Example: myChannelList.ItemsSource = DiscordContext.GetLiveChannelsForGuild(guildId);
    /// </summary>
    public static ObservableConcurrentList<DiscordChannel>? GetLiveChannelsForGuild(ulong guildId)
        => s_liveChannelsForGuildProvider?.Invoke(guildId);

    /// <summary>
    /// Gets the live observable list of members for a specific guild. This list automatically
    /// updates when members join, leave, or are modified, raising INotifyCollectionChanged events.
    /// Returns null if the guild has no cached members.
    /// Example: myMemberList.ItemsSource = DiscordContext.GetLiveMembersForGuild(guildId);
    /// </summary>
    public static ObservableConcurrentList<DiscordMember>? GetLiveMembersForGuild(ulong guildId)
        => s_liveMembersForGuildProvider?.Invoke(guildId);

    /// <summary>All category channels</summary>
    public static IReadOnlyList<DiscordChannel> Categories => Channels.Where(c => c.IsCategory).ToArray();

    /// <summary>All text channels (excludes categories, threads, and voice)</summary>
    public static IReadOnlyList<DiscordChannel> TextChannels => Channels.Where(c => c.IsTextChannel).ToArray();

    /// <summary>All voice channels</summary>
    public static IReadOnlyList<DiscordChannel> VoiceChannels => Channels.Where(c => c.IsVoiceChannel).ToArray();

    /// <summary>All thread channels</summary>
    public static IReadOnlyList<DiscordChannel> Threads => Channels.Where(c => c.IsThread).ToArray();

    /// <summary>Get all channels in a specific guild</summary>
    public static IReadOnlyList<DiscordChannel> GetChannelsInGuild(ulong guildId)
        => Channels.Where(c => c.Guild?.Id == guildId).ToArray();

    /// <summary>Get all category channels in a specific guild</summary>
    public static IReadOnlyList<DiscordChannel> GetCategoriesInGuild(ulong guildId)
        => Channels.Where(c => c.Guild?.Id == guildId && c.IsCategory).ToArray();

    /// <summary>Get all channels within a specific category</summary>
    public static IReadOnlyList<DiscordChannel> GetChannelsInCategory(ulong categoryId)
        => Channels.Where(c => c.Parent_Id == categoryId).ToArray();

    /// <summary>Get all members in a specific guild</summary>
    public static IReadOnlyList<DiscordMember> GetMembersInGuild(ulong guildId)
        => Members.Where(m => m.Guild.Id == guildId).ToArray();

    /// <summary>Get all roles in a specific guild</summary>
    public static IReadOnlyList<DiscordRole> GetRolesInGuild(ulong guildId)
        => Roles.Where(r => r.Guild.Id == guildId).ToArray();

    /// <summary>
    /// If the bot is in exactly one guild, returns that guild. Otherwise returns null.
    /// Useful for single-guild bots to avoid passing guild IDs everywhere.
    /// </summary>
    public static DiscordGuild? Guild => Guilds.Count == 1 ? Guilds[0] : null;

    /// <summary>Find a specific guild by ID (string - parses to ulong)</summary>
    public static DiscordGuild? GetGuild(string guildId)
        => ulong.TryParse(guildId, out ulong id) ? Guilds.FirstOrDefault(g => g.Id == id) : null;

    /// <summary>Find a specific guild by ID (ulong)</summary>
    public static DiscordGuild? GetGuild(ulong guildId)
        => Guilds.FirstOrDefault(g => g.Id == guildId);

    /// <summary>Find a specific guild by ID (ReadOnlySpan&lt;char&gt; - parses to ulong)</summary>
    public static DiscordGuild? GetGuild(ReadOnlySpan<char> guildId)
    {
        if (ulong.TryParse(guildId, out ulong id))
        {
            return Guilds.FirstOrDefault(g => g.Id == id);
        }
        return null;
    }

    /// <summary>Find a specific channel by ID (ulong)</summary>
    public static DiscordChannel? GetChannel(ulong channelId)
        => Channels.FirstOrDefault(c => c.Id == channelId);

    /// <summary>Find a specific member by user ID and guild ID</summary>
    public static DiscordMember? GetMember(ulong guildId, ulong userId)
        => Members.FirstOrDefault(m => m.Guild.Id == guildId && m.User.Id == userId);

    /// <summary>Find a specific role by ID</summary>
    public static DiscordRole? GetRole(ulong roleId)
        => Roles.FirstOrDefault(r => r.Id == roleId);

    internal static void SetProvider(
        IDiscordBot bot,
        DiscordUser? botUser,
        Func<IReadOnlyList<DiscordGuild>> guilds,
        Func<IReadOnlyList<DiscordChannel>> channels,
        Func<IReadOnlyList<DiscordMember>> members,
        Func<IReadOnlyList<DiscordUser>> users,
        Func<IReadOnlyList<DiscordRole>> roles,
        ObservableConcurrentDictionary<ulong, DiscordGuild>? liveGuilds = null,
        Func<IEnumerable<DiscordChannel>>? liveChannels = null,
        Func<IEnumerable<DiscordMember>>? liveMembers = null,
        Func<ulong, ObservableConcurrentList<DiscordChannel>?>? liveChannelsForGuild = null,
        Func<ulong, ObservableConcurrentList<DiscordMember>?>? liveMembersForGuild = null)
    {
        s_operations = new DiscordContextOperations(bot);
        s_botUser = botUser;
        s_guildsProvider = guilds;
        s_channelsProvider = channels;
        s_membersProvider = members;
        s_usersProvider = users;
        s_rolesProvider = roles;

        // Set live collection providers
        s_liveGuilds = liveGuilds;
        s_liveChannelsProvider = liveChannels;
        s_liveMembersProvider = liveMembers;
        s_liveChannelsForGuildProvider = liveChannelsForGuild;
        s_liveMembersForGuildProvider = liveMembersForGuild;
    }

    internal static void ClearProvider()
    {
        s_operations = null;
        s_botUser = null;
        s_guildsProvider = null;
        s_channelsProvider = null;
        s_membersProvider = null;
        s_usersProvider = null;
        s_rolesProvider = null;

        // Clear live collection providers
        s_liveGuilds = null;
        s_liveChannelsProvider = null;
        s_liveMembersProvider = null;
        s_liveChannelsForGuildProvider = null;
        s_liveMembersForGuildProvider = null;
    }
}
