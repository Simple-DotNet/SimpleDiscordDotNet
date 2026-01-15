using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Primitives;

namespace SimpleDiscordNet.Context;

/// <summary>
/// Safe wrapper around IDiscordBot that only exposes messaging and read-only operations.
/// Used by DiscordContext to prevent unsafe operations like starting/stopping the bot.
/// </summary>
internal sealed class DiscordContextOperations : IDiscordContextOperations
{
    private readonly IDiscordBot _bot;

    public DiscordContextOperations(IDiscordBot bot)
    {
        _bot = bot;
    }

    public Task<DiscordMessage?> SendMessageAsync(string channelId, string content, EmbedBuilder? embed = null, CancellationToken ct = default)
        => _bot.SendMessageAsync(channelId, content, embed, ct);

    public Task<DiscordMessage?> SendMessageAsync(ulong channelId, string content, EmbedBuilder? embed = null, CancellationToken ct = default)
        => _bot.SendMessageAsync(channelId, content, embed, ct);

    public Task<DiscordMessage?> SendMessageAsync(DiscordChannel channel, string content, EmbedBuilder? embed = null, CancellationToken ct = default)
        => _bot.SendMessageAsync(channel, content, embed, ct);

    public Task<DiscordMessage?> SendMessageAsync(string channelId, MessageBuilder builder, CancellationToken ct = default)
        => _bot.SendMessageAsync(channelId, builder, ct);

    public Task<DiscordMessage?> SendMessageAsync(ulong channelId, MessageBuilder builder, CancellationToken ct = default)
        => _bot.SendMessageAsync(channelId, builder, ct);

    public Task<DiscordMessage?> SendMessageAsync(DiscordChannel channel, MessageBuilder builder, CancellationToken ct = default)
        => _bot.SendMessageAsync(channel, builder, ct);

    public Task<DiscordMessage?> SendAttachmentAsync(string channelId, string content, string fileName, ReadOnlyMemory<byte> data, EmbedBuilder? embed = null, CancellationToken ct = default)
        => _bot.SendAttachmentAsync(channelId, content, fileName, data, embed, ct);

    public Task<DiscordMessage?> SendAttachmentAsync(ulong channelId, string content, string fileName, ReadOnlyMemory<byte> data, EmbedBuilder? embed = null, CancellationToken ct = default)
        => _bot.SendAttachmentAsync(channelId, content, fileName, data, embed, ct);

    public Task<DiscordMessage?> SendAttachmentAsync(DiscordChannel channel, string content, string fileName, ReadOnlyMemory<byte> data, EmbedBuilder? embed = null, CancellationToken ct = default)
        => _bot.SendAttachmentAsync(channel, content, fileName, data, embed, ct);

    public Task<DiscordGuild?> GetGuildAsync(string guildId, CancellationToken ct = default, bool useCache = true)
        => _bot.GetGuildAsync(guildId, ct, useCache);

    public Task<DiscordGuild?> GetGuildAsync(ulong guildId, CancellationToken ct = default, bool useCache = true)
        => _bot.GetGuildAsync(guildId, ct, useCache);

    public Task<IEnumerable<DiscordChannel>> GetGuildChannelsAsync(string guildId, CancellationToken ct = default, bool useCache = true)
        => _bot.GetGuildChannelsAsync(guildId, ct, useCache);

    public Task<IEnumerable<DiscordChannel>> GetGuildChannelsAsync(ulong guildId, CancellationToken ct = default, bool useCache = true)
        => _bot.GetGuildChannelsAsync(guildId, ct, useCache);

    public Task<IEnumerable<DiscordChannel>> GetGuildChannelsAsync(DiscordGuild guild, CancellationToken ct = default, bool useCache = true)
        => _bot.GetGuildChannelsAsync(guild, ct, useCache);

    public Task<IEnumerable<DiscordRole>> GetGuildRolesAsync(string guildId, CancellationToken ct = default, bool useCache = true)
        => _bot.GetGuildRolesAsync(guildId, ct, useCache);

    public Task<IEnumerable<DiscordRole>> GetGuildRolesAsync(ulong guildId, CancellationToken ct = default, bool useCache = true)
        => _bot.GetGuildRolesAsync(guildId, ct, useCache);

    public Task<IEnumerable<DiscordRole>> GetGuildRolesAsync(DiscordGuild guild, CancellationToken ct = default, bool useCache = true)
        => _bot.GetGuildRolesAsync(guild, ct, useCache);

    public Task<IEnumerable<DiscordMember>> ListGuildMembersAsync(string guildId, int limit = 1000, string? after = null, CancellationToken ct = default)
        => _bot.ListGuildMembersAsync(guildId, limit, after, ct);

    public Task<IEnumerable<DiscordMember>> ListGuildMembersAsync(ulong guildId, int limit = 1000, string? after = null, CancellationToken ct = default)
        => _bot.ListGuildMembersAsync(guildId, limit, after, ct);

    public Task<IEnumerable<DiscordMember>> ListGuildMembersAsync(DiscordGuild guild, int limit = 1000, string? after = null, CancellationToken ct = default)
        => _bot.ListGuildMembersAsync(guild, limit, after, ct);

    public Task AddRoleToMemberAsync(string guildId, string userId, string roleId, CancellationToken ct = default)
        => _bot.AddRoleToMemberAsync(guildId, userId, roleId, ct);

    public Task RemoveRoleFromMemberAsync(string guildId, string userId, string roleId, CancellationToken ct = default)
        => _bot.RemoveRoleFromMemberAsync(guildId, userId, roleId, ct);

    public Task TimeoutMemberAsync(string guildId, string userId, TimeSpan duration, CancellationToken ct = default)
        => _bot.TimeoutMemberAsync(guildId, userId, duration, ct);
    public Task TimeoutMemberAsync(ulong guildId, ulong userId, TimeSpan duration, CancellationToken ct = default)
        => _bot.TimeoutMemberAsync(guildId, userId, duration, ct);

    public Task RemoveTimeoutMemberAsync(string guildId, string userId, CancellationToken ct = default)
        => _bot.RemoveTimeoutMemberAsync(guildId, userId, ct);
    public Task RemoveTimeoutMemberAsync(ulong guildId, ulong userId, CancellationToken ct = default)
        => _bot.RemoveTimeoutMemberAsync(guildId, userId, ct);

    public Task MuteMemberAsync(string guildId, string userId, CancellationToken ct = default)
        => _bot.MuteMemberAsync(guildId, userId, ct);
    public Task MuteMemberAsync(ulong guildId, ulong userId, CancellationToken ct = default)
        => _bot.MuteMemberAsync(guildId, userId, ct);

    public Task UnmuteMemberAsync(string guildId, string userId, CancellationToken ct = default)
        => _bot.UnmuteMemberAsync(guildId, userId, ct);
    public Task UnmuteMemberAsync(ulong guildId, ulong userId, CancellationToken ct = default)
        => _bot.UnmuteMemberAsync(guildId, userId, ct);

    public Task<DiscordMessage?> SendDMAsync(string userId, string content, EmbedBuilder? embed = null, CancellationToken ct = default)
        => _bot.SendDMAsync(userId, content, embed, ct);

    public Task PinMessageAsync(ulong channelId, ulong messageId, CancellationToken ct = default)
        => _bot.PinMessageAsync(channelId, messageId, ct);

    public Task DeleteMessageAsync(ulong channelId, ulong messageId, CancellationToken ct = default)
        => _bot.DeleteMessageAsync(channelId, messageId, ct);

    public Task<DiscordChannel?> CreateChannelAsync(ulong guildId, string name, ChannelType type, string? parentId = null, object[]? permissionOverwrites = null, CancellationToken ct = default)
        => _bot.CreateChannelAsync(guildId.ToString(), name, type, parentId, permissionOverwrites, ct);

    public Task DeleteChannelAsync(ulong channelId, CancellationToken ct = default)
        => _bot.DeleteChannelAsync(channelId.ToString(), ct);

    public Task<DiscordChannel?> ModifyChannelAsync(ulong channelId, string? name = null, string? parentId = null, int? position = null, string? topic = null, bool? nsfw = null, int? bitrate = null, int? userLimit = null, int? rateLimitPerUser = null, CancellationToken ct = default)
        => _bot.ModifyChannelAsync(channelId.ToString(), name, null, parentId, position, topic, nsfw, bitrate, userLimit, rateLimitPerUser, ct);

    public Task SetChannelPermissionAsync(string channelId, string targetId, int type, ulong allow, ulong deny, CancellationToken ct = default)
        => _bot.SetChannelPermissionAsync(channelId, targetId, type, allow, deny, ct);

    public Task DeleteChannelPermissionAsync(string channelId, string overwriteId, CancellationToken ct = default)
        => _bot.DeleteChannelPermissionAsync(channelId, overwriteId, ct);

    public Task<IEnumerable<DiscordMessage>> GetMessagesAsync(string channelId, int limit = 50, string? before = null, string? after = null, CancellationToken ct = default)
        => _bot.GetMessagesAsync(channelId, limit, before, after, ct);

    public Task BulkDeleteMessagesAsync(string channelId, string[] messageIds, CancellationToken ct = default)
        => _bot.BulkDeleteMessagesAsync(channelId, messageIds, ct);
}
