using System.Globalization;
using SimpleDiscordNet.Primitives;

namespace SimpleDiscordNet.Entities;

/// <summary>Discord channel types</summary>
public enum ChannelType
{
    GuildText = 0,
    DM = 1,
    GuildVoice = 2,
    GroupDM = 3,
    GuildCategory = 4,
    GuildAnnouncement = 5,
    AnnouncementThread = 10,
    PublicThread = 11,
    PrivateThread = 12,
    GuildStageVoice = 13,
    GuildDirectory = 14,
    GuildForum = 15,
    GuildMedia = 16
}

public sealed class DiscordChannel
{
    public required ulong Id { get; init; }
    public required string Name { get; init; }
    public required int Type { get; init; }

    /// <summary>The guild this channel belongs to. Null for DM channels.</summary>
    public DiscordGuild? Guild { get; internal set; }

    public ulong? Parent_Id { get; internal set; }
    public ulong? Guild_Id { get; internal set; }
    public ChannelPermissionOverwrite[]? Permission_Overwrites { get; internal set; }

    /// <summary>Gets the channel type as an enum</summary>
    public ChannelType ChannelType => (ChannelType)Type;

    /// <summary>Returns true if this channel is a category (Type = 4)</summary>
    public bool IsCategory => Type == (int)Entities.ChannelType.GuildCategory;

    /// <summary>Returns true if this channel is a text channel (Type = 0)</summary>
    public bool IsTextChannel => Type == (int)Entities.ChannelType.GuildText;

    /// <summary>Returns true if this channel is a voice channel (Type = 2)</summary>
    public bool IsVoiceChannel => Type == (int)Entities.ChannelType.GuildVoice;

    /// <summary>Returns true if this channel is a thread (Types 10, 11, 12)</summary>
    public bool IsThread => Type is (int)Entities.ChannelType.AnnouncementThread or (int)Entities.ChannelType.PublicThread or (int)Entities.ChannelType.PrivateThread;

    /// <summary>Returns true if this channel is in a category (has Parent_Id)</summary>
    public bool HasParent => Parent_Id.HasValue;

    /// <summary>Gets the permission overwrite for a specific role or member ID, or null if not found</summary>
    public ChannelPermissionOverwrite? GetOverwrite(ulong id)
        => Permission_Overwrites?.FirstOrDefault(o => o.Id == id);

    /// <summary>Gets all role permission overwrites</summary>
    public IEnumerable<ChannelPermissionOverwrite> GetRoleOverwrites()
        => Permission_Overwrites?.Where(o => o.IsRole) ?? [];

    /// <summary>Gets all member permission overwrites</summary>
    public IEnumerable<ChannelPermissionOverwrite> GetMemberOverwrites()
        => Permission_Overwrites?.Where(o => o.IsMember) ?? [];

    /// <summary>
    /// Sends a message to this channel.
    /// Example: await channel.SendMessageAsync("Hello!");
    /// </summary>
    public Task<DiscordMessage?> SendMessageAsync(string content, EmbedBuilder? embed = null, CancellationToken ct = default)
        => Context.DiscordContext.Operations.SendMessageAsync(Id.ToString(CultureInfo.InvariantCulture), content, embed, ct);

    /// <summary>
    /// Sends a message to this channel using a MessageBuilder.
    /// Example: await channel.SendMessageAsync(new MessageBuilder().WithContent("Hello").WithEmbed(embed));
    /// </summary>
    public Task<DiscordMessage?> SendMessageAsync(MessageBuilder builder, CancellationToken ct = default)
        => Context.DiscordContext.Operations.SendMessageAsync(Id.ToString(CultureInfo.InvariantCulture), builder, ct);

    /// <summary>
    /// Deletes this channel.
    /// Example: await channel.DeleteAsync();
    /// </summary>
    public Task DeleteAsync(CancellationToken ct = default)
        => Context.DiscordContext.Operations.DeleteChannelAsync(Id, ct);

    /// <summary>
    /// Moves this channel to a different category or changes its position.
    /// Example: await channel.MoveAsync(parentId: categoryId);
    /// Example: await channel.MoveAsync(position: 5);
    /// </summary>
    public Task<DiscordChannel?> MoveAsync(string? parentId = null, int? position = null, CancellationToken ct = default)
        => Context.DiscordContext.Operations.ModifyChannelAsync(Id, name: null, parentId: parentId, position: position, ct: ct);

    /// <summary>
    /// Sets the channel topic (text channels only).
    /// Example: await channel.SetTopicAsync("Welcome to the channel!");
    /// </summary>
    public Task<DiscordChannel?> SetTopicAsync(string topic, CancellationToken ct = default)
        => Context.DiscordContext.Operations.ModifyChannelAsync(Id, topic: topic, ct: ct);

    /// <summary>
    /// Sets the channel name.
    /// Example: await channel.SetNameAsync("new-channel-name");
    /// </summary>
    public Task<DiscordChannel?> SetNameAsync(string name, CancellationToken ct = default)
        => Context.DiscordContext.Operations.ModifyChannelAsync(Id, name: name, ct: ct);

    /// <summary>
    /// Sets the NSFW flag for this channel.
    /// Example: await channel.SetNsfwAsync(true);
    /// </summary>
    public Task<DiscordChannel?> SetNsfwAsync(bool nsfw, CancellationToken ct = default)
        => Context.DiscordContext.Operations.ModifyChannelAsync(Id, nsfw: nsfw, ct: ct);

    /// <summary>
    /// Sets the bitrate for voice channels (in bits per second, min 8000).
    /// Example: await voiceChannel.SetBitrateAsync(96000);
    /// </summary>
    public Task<DiscordChannel?> SetBitrateAsync(int bitrate, CancellationToken ct = default)
        => Context.DiscordContext.Operations.ModifyChannelAsync(Id, bitrate: bitrate, ct: ct);

    /// <summary>
    /// Sets the user limit for voice channels (0 = unlimited).
    /// Example: await voiceChannel.SetUserLimitAsync(10);
    /// </summary>
    public Task<DiscordChannel?> SetUserLimitAsync(int userLimit, CancellationToken ct = default)
        => Context.DiscordContext.Operations.ModifyChannelAsync(Id, userLimit: userLimit, ct: ct);

    /// <summary>
    /// Sets the slowmode rate limit (seconds between messages, 0-21600).
    /// Example: await channel.SetSlowmodeAsync(5); // 5 seconds between messages
    /// </summary>
    public Task<DiscordChannel?> SetSlowmodeAsync(int rateLimitPerUser, CancellationToken ct = default)
        => Context.DiscordContext.Operations.ModifyChannelAsync(Id, rateLimitPerUser: rateLimitPerUser, ct: ct);

    /// <summary>
    /// Modifies channel permissions for a specific role or member.
    /// This sets explicit allow and deny permissions. Use bitwise OR to combine multiple permissions.
    /// Example: await channel.ModifyPermissionsAsync(roleId, allow: PermissionFlags.SendMessages | PermissionFlags.ViewChannel);
    /// </summary>
    /// <param name="targetId">Role ID or User ID</param>
    /// <param name="allow">Permissions to explicitly allow (optional)</param>
    /// <param name="deny">Permissions to explicitly deny (optional)</param>
    /// <param name="isRole">True for role permissions, false for member permissions (default: true)</param>
    public async Task ModifyPermissionsAsync(ulong targetId, PermissionFlags? allow = null, PermissionFlags? deny = null, bool isRole = true, CancellationToken ct = default)
    {
        // Get current overwrite for this target
        var current = GetOverwrite(targetId);
        ulong allowBits = allow.HasValue ? (ulong)allow.Value : (current?.Allow ?? 0);
        ulong denyBits = deny.HasValue ? (ulong)deny.Value : (current?.Deny ?? 0);

        await Context.DiscordContext.Operations.SetChannelPermissionAsync(
            Id.ToString(CultureInfo.InvariantCulture),
            targetId.ToString(CultureInfo.InvariantCulture),
            isRole ? 0 : 1,
            allowBits,
            denyBits,
            ct);
    }

    /// <summary>
    /// Adds a single permission for a specific role or member.
    /// This adds the permission to the allow list without affecting other permissions.
    /// Example: await channel.AddPermissionAsync(roleId, PermissionFlags.AttachFiles);
    /// </summary>
    /// <param name="targetId">Role ID or User ID</param>
    /// <param name="permission">The permission to add</param>
    /// <param name="isRole">True for role permissions, false for member permissions (default: true)</param>
    public async Task AddPermissionAsync(ulong targetId, PermissionFlags permission, bool isRole = true, CancellationToken ct = default)
    {
        var current = GetOverwrite(targetId);
        ulong allowBits = (current?.Allow ?? 0) | (ulong)permission;
        ulong denyBits = (current?.Deny ?? 0) & ~(ulong)permission; // Remove from deny if present

        await Context.DiscordContext.Operations.SetChannelPermissionAsync(
            Id.ToString(CultureInfo.InvariantCulture),
            targetId.ToString(CultureInfo.InvariantCulture),
            isRole ? 0 : 1,
            allowBits,
            denyBits,
            ct);
    }

    /// <summary>
    /// Removes a single permission for a specific role or member.
    /// This removes the permission from both allow and deny lists, returning it to the default/inherited state.
    /// Example: await channel.RemovePermissionAsync(roleId, PermissionFlags.AttachFiles);
    /// </summary>
    /// <param name="targetId">Role ID or User ID</param>
    /// <param name="permission">The permission to remove</param>
    /// <param name="isRole">True for role permissions, false for member permissions (default: true)</param>
    public async Task RemovePermissionAsync(ulong targetId, PermissionFlags permission, bool isRole = true, CancellationToken ct = default)
    {
        var current = GetOverwrite(targetId);
        if (current == null) return; // No overwrite exists, nothing to remove

        ulong allowBits = current.Allow & ~(ulong)permission;
        ulong denyBits = current.Deny & ~(ulong)permission;

        await Context.DiscordContext.Operations.SetChannelPermissionAsync(
            Id.ToString(CultureInfo.InvariantCulture),
            targetId.ToString(CultureInfo.InvariantCulture),
            isRole ? 0 : 1,
            allowBits,
            denyBits,
            ct);
    }

    /// <summary>
    /// Denies a single permission for a specific role or member.
    /// This explicitly denies the permission, overriding any role-based allows.
    /// Example: await channel.DenyPermissionAsync(roleId, PermissionFlags.SendMessages);
    /// </summary>
    /// <param name="targetId">Role ID or User ID</param>
    /// <param name="permission">The permission to deny</param>
    /// <param name="isRole">True for role permissions, false for member permissions (default: true)</param>
    public async Task DenyPermissionAsync(ulong targetId, PermissionFlags permission, bool isRole = true, CancellationToken ct = default)
    {
        var current = GetOverwrite(targetId);
        ulong allowBits = (current?.Allow ?? 0) & ~(ulong)permission; // Remove from allow if present
        ulong denyBits = (current?.Deny ?? 0) | (ulong)permission;

        await Context.DiscordContext.Operations.SetChannelPermissionAsync(
            Id.ToString(CultureInfo.InvariantCulture),
            targetId.ToString(CultureInfo.InvariantCulture),
            isRole ? 0 : 1,
            allowBits,
            denyBits,
            ct);
    }

    /// <summary>
    /// Deletes all permission overwrites for a specific role or member.
    /// This resets the target to use default/inherited permissions.
    /// Example: await channel.DeletePermissionOverwriteAsync(roleId);
    /// </summary>
    /// <param name="targetId">Role ID or User ID</param>
    public Task DeletePermissionOverwriteAsync(ulong targetId, CancellationToken ct = default)
        => Context.DiscordContext.Operations.DeleteChannelPermissionAsync(Id.ToString(CultureInfo.InvariantCulture), targetId.ToString(CultureInfo.InvariantCulture), ct);

    /// <summary>
    /// Gets recent messages from this channel (up to 100).
    /// Example: foreach (var message in await channel.GetMessagesAsync(50)) { }
    /// </summary>
    /// <param name="limit">Number of messages to retrieve (1-100)</param>
    /// <param name="before">Get messages before this message ID (optional)</param>
    /// <param name="after">Get messages after this message ID (optional)</param>
    public Task<IEnumerable<DiscordMessage>> GetMessagesAsync(int limit, string? before = null, string? after = null, CancellationToken ct = default)
        => Context.DiscordContext.Operations.GetMessagesAsync(Id.ToString(CultureInfo.InvariantCulture), limit, before, after, ct);

    /// <summary>
    /// Bulk deletes the most recent messages in this channel (2-100 messages, must be less than 14 days old).
    /// Example: await channel.BulkDeleteMessagesAsync(50);
    /// </summary>
    /// <param name="count">Number of recent messages to delete (2-100)</param>
    public async Task BulkDeleteMessagesAsync(int count, CancellationToken ct = default)
    {
        if (count < 2 || count > 100)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be between 2 and 100");

        var messages = await GetMessagesAsync(count, ct: ct);
        var messageIds = messages.Select(static m => m.Id.ToString(CultureInfo.InvariantCulture)).ToArray();

        if (messageIds.Length == 0)
            return;

        await Context.DiscordContext.Operations.BulkDeleteMessagesAsync(Id.ToString(CultureInfo.InvariantCulture), messageIds, ct);
    }
}

public sealed record ChannelPermissionOverwrite
{
    public required ulong Id { get; init; }
    /// <summary>0 = role, 1 = member</summary>
    public required int Type { get; init; }
    /// <summary>Permission bitset for allowed permissions</summary>
    public required ulong Allow { get; init; }
    /// <summary>Permission bitset for denied permissions</summary>
    public required ulong Deny { get; init; }

    /// <summary>Returns true if this overwriting is for a role (Type = 0)</summary>
    public bool IsRole => Type == 0;
    /// <summary>Returns true if this overwriting is for a member (Type = 1)</summary>
    public bool IsMember => Type == 1;

    /// <summary>Checks if a specific permission is explicitly allowed</summary>
    public bool HasAllow(PermissionFlags permission) => (Allow & (ulong)permission) != 0;
    /// <summary>Checks if a specific permission is explicitly denied</summary>
    public bool HasDeny(PermissionFlags permission) => (Deny & (ulong)permission) != 0;
}
