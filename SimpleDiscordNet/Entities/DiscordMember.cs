namespace SimpleDiscordNet.Entities;

public sealed class DiscordMember
{
    public required DiscordUser User { get; init; }

    /// <summary>The guild this member belongs to</summary>
    public required DiscordGuild Guild { get; init; }

    public string? Nick { get; internal set; }
    public ulong[] Roles { get; internal set; } = [];

    /// <summary>Guild-specific avatar hash</summary>
    public string? Avatar { get; internal set; }

    /// <summary>When the user joined the guild (ISO8601 timestamp)</summary>
    public string? Joined_At { get; internal set; }

    /// <summary>When the user started boosting the guild (ISO8601 timestamp)</summary>
    public string? Premium_Since { get; internal set; }

    /// <summary>Whether the user is deafened in voice channels</summary>
    public bool? Deaf { get; internal set; }

    /// <summary>Whether the user is muted in voice channels</summary>
    public bool? Mute { get; internal set; }

    /// <summary>Member flags (bit set)</summary>
    public int? Flags { get; internal set; }

    /// <summary>Whether the user has not yet passed the guild's Membership Screening requirements</summary>
    public bool? Pending { get; internal set; }

    /// <summary>Total permissions of the member in the channel (for interaction contexts)</summary>
    public ulong? Permissions { get; internal set; }

    /// <summary>When the user's timeout will expire (ISO8601 timestamp). Null if not timed out.</summary>
    public string? Communication_Disabled_Until { get; internal set; }

    /// <summary>
    /// Gets the member's display name (nickname if set, otherwise user's display name).
    /// </summary>
    public string DisplayName => Nick ?? User.DisplayName;

    /// <summary>
    /// Gets the member's guild-specific avatar URL. Returns null if no guild avatar.
    /// </summary>
    /// <param name="size">Image size (power of 2, between 16 and 4096)</param>
    /// <param name="format">Image format (png, jpg, webp, gif)</param>
    public string? GetGuildAvatarUrl(int size = 256, string format = "png")
    {
        return string.IsNullOrEmpty(Avatar) ? null : $"https://cdn.discordapp.com/guilds/{Guild.Id}/users/{User.Id}/avatars/{Avatar}.{format}?size={size}";
    }

    /// <summary>
    /// Gets the effective avatar URL (guild avatar if available, otherwise user avatar).
    /// </summary>
    public string? GetEffectiveAvatarUrl(int size = 256, string format = "png")
        => GetGuildAvatarUrl(size, format) ?? User.GetAvatarUrl(size, format) ?? User.GetDefaultAvatarUrl();

    /// <summary>
    /// Checks if this member has a specific role.
    /// </summary>
    public bool HasRole(ulong roleId) => Roles.Contains(roleId);

    /// <summary>
    /// Adds a role to this member.
    /// Example: await member.AddRoleAsync(roleId);
    /// </summary>
    public Task AddRoleAsync(ulong roleId, CancellationToken ct = default)
        => Context.DiscordContext.Operations.AddRoleToMemberAsync(Guild.Id.ToString(), User.Id.ToString(), roleId.ToString(), ct);

    /// <summary>
    /// Removes a role from this member.
    /// Example: await member.RemoveRoleAsync(roleId);
    /// </summary>
    public Task RemoveRoleAsync(ulong roleId, CancellationToken ct = default)
        => Context.DiscordContext.Operations.RemoveRoleFromMemberAsync(Guild.Id.ToString(), User.Id.ToString(), roleId.ToString(), ct);

    /// <summary>
    /// Checks if the member is currently timed out.
    /// </summary>
    public bool IsTimedOut
    {
        get
        {
            if (string.IsNullOrEmpty(Communication_Disabled_Until)) return false;
            if (DateTimeOffset.TryParse(Communication_Disabled_Until, out DateTimeOffset until))
                return until > DateTimeOffset.UtcNow;
            return false;
        }
    }

    /// <summary>
    /// Checks if the member has a specific permission (for interaction contexts).
    /// </summary>
    public bool HasPermission(PermissionFlags permission) => Permissions.HasValue && (Permissions.Value & (ulong)permission) != 0;

    /// <summary>
    /// Returns true if this member's user is a bot account.
    /// </summary>
    public bool IsBot => User.IsBot;

    /// <summary>
    /// Returns true if this member is the currently running bot (checks against DiscordContext.BotUser).
    /// Use this to ignore the bot's own actions/events.
    /// Example: if (member.IsCurrentBot) return; // Ignore self
    /// </summary>
    public bool IsCurrentBot => User.IsCurrentBot;

    /// <summary>
    /// Adds a channel permission for this member.
    /// Example: await member.AddChannelPermissionAsync(channel, PermissionFlags.AttachFiles);
    /// </summary>
    public Task AddChannelPermissionAsync(DiscordChannel channel, PermissionFlags permission, CancellationToken ct = default)
        => channel.AddPermissionAsync(User.Id, permission, isRole: false, ct);

    /// <summary>
    /// Removes a channel permission for this member.
    /// Example: await member.RemoveChannelPermissionAsync(channel, PermissionFlags.AttachFiles);
    /// </summary>
    public Task RemoveChannelPermissionAsync(DiscordChannel channel, PermissionFlags permission, CancellationToken ct = default)
        => channel.RemovePermissionAsync(User.Id, permission, isRole: false, ct);

    /// <summary>
    /// Denies a channel permission for this member.
    /// Example: await member.DenyChannelPermissionAsync(channel, PermissionFlags.SendMessages);
    /// </summary>
    public Task DenyChannelPermissionAsync(DiscordChannel channel, PermissionFlags permission, CancellationToken ct = default)
        => channel.DenyPermissionAsync(User.Id, permission, isRole: false, ct);

    /// <summary>
    /// Modifies channel permissions for this member.
    /// Example: await member.ModifyChannelPermissionsAsync(channel, allow: PermissionFlags.SendMessages | PermissionFlags.ViewChannel);
    /// </summary>
    public Task ModifyChannelPermissionsAsync(DiscordChannel channel, PermissionFlags? allow = null, PermissionFlags? deny = null, CancellationToken ct = default)
        => channel.ModifyPermissionsAsync(User.Id, allow, deny, isRole: false, ct);

    /// <summary>
    /// Deletes all channel permission overwrites for this member.
    /// Example: await member.DeleteChannelPermissionOverwriteAsync(channel);
    /// </summary>
    public Task DeleteChannelPermissionOverwriteAsync(DiscordChannel channel, CancellationToken ct = default)
        => channel.DeletePermissionOverwriteAsync(User.Id, ct);
}
