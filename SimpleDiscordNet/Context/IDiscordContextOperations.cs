using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Primitives;

namespace SimpleDiscordNet.Context;

/// <summary>
/// Safe subset of bot operations available through DiscordContext.
/// Only includes messaging and read-only operations - no lifecycle or configuration methods.
/// </summary>
public interface IDiscordContextOperations
{
    /// <summary>
    /// Sends a simple text message to the specified channel.
    /// </summary>
    Task<DiscordMessage?> SendMessageAsync(string channelId, string content, EmbedBuilder? embed = null, CancellationToken ct = default);
    Task<DiscordMessage?> SendMessageAsync(ulong channelId, string content, EmbedBuilder? embed = null, CancellationToken ct = default);
    Task<DiscordMessage?> SendMessageAsync(DiscordChannel channel, string content, EmbedBuilder? embed = null, CancellationToken ct = default);

    /// <summary>
    /// Sends a message using a MessageBuilder to the specified channel.
    /// </summary>
    Task<DiscordMessage?> SendMessageAsync(string channelId, MessageBuilder builder, CancellationToken ct = default);
    Task<DiscordMessage?> SendMessageAsync(ulong channelId, MessageBuilder builder, CancellationToken ct = default);
    Task<DiscordMessage?> SendMessageAsync(DiscordChannel channel, MessageBuilder builder, CancellationToken ct = default);

    /// <summary>
    /// Sends a message with a single file attachment to the specified channel.
    /// </summary>
    Task<DiscordMessage?> SendAttachmentAsync(string channelId, string content, string fileName, ReadOnlyMemory<byte> data, EmbedBuilder? embed = null, CancellationToken ct = default);
    Task<DiscordMessage?> SendAttachmentAsync(ulong channelId, string content, string fileName, ReadOnlyMemory<byte> data, EmbedBuilder? embed = null, CancellationToken ct = default);
    Task<DiscordMessage?> SendAttachmentAsync(DiscordChannel channel, string content, string fileName, ReadOnlyMemory<byte> data, EmbedBuilder? embed = null, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a guild by its id.
    /// </summary>
    /// <param name="useCache">Whether to attempt to retrieve the guild from cache first (default: true). If false, always fetches from Discord API.</param>
    Task<DiscordGuild?> GetGuildAsync(string guildId, CancellationToken ct = default, bool useCache = true);
    Task<DiscordGuild?> GetGuildAsync(ulong guildId, CancellationToken ct = default, bool useCache = true);

    /// <summary>
    /// Retrieves all channels for a guild.
    /// </summary>
    /// <param name="useCache">Whether to attempt to retrieve channels from cache first (default: true). If false, always fetches from Discord API.</param>
    Task<IEnumerable<DiscordChannel>> GetGuildChannelsAsync(string guildId, CancellationToken ct = default, bool useCache = true);
    Task<IEnumerable<DiscordChannel>> GetGuildChannelsAsync(ulong guildId, CancellationToken ct = default, bool useCache = true);
    Task<IEnumerable<DiscordChannel>> GetGuildChannelsAsync(DiscordGuild guild, CancellationToken ct = default, bool useCache = true);

    /// <summary>
    /// Retrieves all roles for a guild.
    /// </summary>
    /// <param name="useCache">Whether to attempt to retrieve roles from cache first (default: true). If false, always fetches from Discord API.</param>
    Task<IEnumerable<DiscordRole>> GetGuildRolesAsync(string guildId, CancellationToken ct = default, bool useCache = true);
    Task<IEnumerable<DiscordRole>> GetGuildRolesAsync(ulong guildId, CancellationToken ct = default, bool useCache = true);
    Task<IEnumerable<DiscordRole>> GetGuildRolesAsync(DiscordGuild guild, CancellationToken ct = default, bool useCache = true);

    /// <summary>
    /// Lists members of a guild with pagination support.
    /// </summary>
    Task<IEnumerable<DiscordMember>> ListGuildMembersAsync(string guildId, int limit = 1000, string? after = null, CancellationToken ct = default);
    Task<IEnumerable<DiscordMember>> ListGuildMembersAsync(ulong guildId, int limit = 1000, string? after = null, CancellationToken ct = default);
    Task<IEnumerable<DiscordMember>> ListGuildMembersAsync(DiscordGuild guild, int limit = 1000, string? after = null, CancellationToken ct = default);

    /// <summary>
    /// Adds a role to a guild member.
    /// </summary>
    Task AddRoleToMemberAsync(string guildId, string userId, string roleId, CancellationToken ct = default);

    /// <summary>
    /// Removes a role from a guild member.
    /// </summary>
    Task RemoveRoleFromMemberAsync(string guildId, string userId, string roleId, CancellationToken ct = default);

    /// <summary>
    /// Timeouts a guild member for a specified duration.
    /// </summary>
    Task TimeoutMemberAsync(string guildId, string userId, TimeSpan duration, CancellationToken ct = default);
    Task TimeoutMemberAsync(ulong guildId, ulong userId, TimeSpan duration, CancellationToken ct = default);

    /// <summary>
    /// Removes a timeout from a guild member.
    /// </summary>
    Task RemoveTimeoutMemberAsync(string guildId, string userId, CancellationToken ct = default);
    Task RemoveTimeoutMemberAsync(ulong guildId, ulong userId, CancellationToken ct = default);

    /// <summary>
    /// Mutes a guild member in voice channels.
    /// </summary>
    Task MuteMemberAsync(string guildId, string userId, CancellationToken ct = default);
    Task MuteMemberAsync(ulong guildId, ulong userId, CancellationToken ct = default);

    /// <summary>
    /// Unmutes a guild member in voice channels.
    /// </summary>
    Task UnmuteMemberAsync(string guildId, string userId, CancellationToken ct = default);
    Task UnmuteMemberAsync(ulong guildId, ulong userId, CancellationToken ct = default);

    /// <summary>
    /// Sends a direct message to a user by creating a DM channel and sending a message.
    /// </summary>
    Task<DiscordMessage?> SendDMAsync(string userId, string content, EmbedBuilder? embed = null, CancellationToken ct = default);

    /// <summary>
    /// Pins a message in a channel.
    /// </summary>
    Task PinMessageAsync(ulong channelId, ulong messageId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a message from a channel.
    /// </summary>
    Task DeleteMessageAsync(ulong channelId, ulong messageId, CancellationToken ct = default);

    /// <summary>
    /// Creates a new channel in a guild.
    /// </summary>
    Task<DiscordChannel?> CreateChannelAsync(ulong guildId, string name, ChannelType type, string? parentId = null, object[]? permissionOverwrites = null, CancellationToken ct = default);

    /// <summary>
    /// Deletes a channel.
    /// </summary>
    Task DeleteChannelAsync(ulong channelId, CancellationToken ct = default);

    /// <summary>
    /// Modifies a channel (name, parent category, position, topic, nsfw, etc.).
    /// </summary>
    Task<DiscordChannel?> ModifyChannelAsync(ulong channelId, string? name = null, string? parentId = null, int? position = null, string? topic = null, bool? nsfw = null, int? bitrate = null, int? userLimit = null, int? rateLimitPerUser = null, CancellationToken ct = default);

    /// <summary>
    /// Sets or updates a channel permission overwrite for a role or member.
    /// </summary>
    Task SetChannelPermissionAsync(string channelId, string targetId, int type, ulong allow, ulong deny, CancellationToken ct = default);

    /// <summary>
    /// Deletes a channel permission overwrite.
    /// </summary>
    Task DeleteChannelPermissionAsync(string channelId, string overwriteId, CancellationToken ct = default);

    /// <summary>
    /// Gets recent messages from a channel (up to 100).
    /// </summary>
    Task<IEnumerable<DiscordMessage>> GetMessagesAsync(string channelId, int limit = 50, string? before = null, string? after = null, CancellationToken ct = default);

    /// <summary>
    /// Bulk deletes multiple messages (2-100 messages, must be less than 14 days old).
    /// </summary>
    Task BulkDeleteMessagesAsync(string channelId, string[] messageIds, CancellationToken ct = default);
}
