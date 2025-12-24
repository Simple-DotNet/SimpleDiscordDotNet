namespace SimpleDiscordNet.Entities;

public sealed class DiscordRole
{
    public required ulong Id { get; init; }
    public required string Name { get; init; }

    /// <summary>The guild this role belongs to</summary>
    public required DiscordGuild Guild { get; init; }

    public int Color { get; internal set; }
    public int Position { get; internal set; }
    public ulong Permissions { get; internal set; }

    /// <summary>Checks if this role has a specific permission</summary>
    public bool HasPermission(PermissionFlags permission) => (Permissions & (ulong)permission) != 0;

    /// <summary>Returns true if this role has Administrator permission (grants all permissions)</summary>
    public bool IsAdministrator => HasPermission(PermissionFlags.Administrator);

    /// <summary>
    /// Adds a channel permission for this role.
    /// Example: await role.AddChannelPermissionAsync(channel, PermissionFlags.AttachFiles);
    /// </summary>
    public Task AddChannelPermissionAsync(DiscordChannel channel, PermissionFlags permission, CancellationToken ct = default)
        => channel.AddPermissionAsync(Id, permission, isRole: true, ct);

    /// <summary>
    /// Removes a channel permission for this role.
    /// Example: await role.RemoveChannelPermissionAsync(channel, PermissionFlags.AttachFiles);
    /// </summary>
    public Task RemoveChannelPermissionAsync(DiscordChannel channel, PermissionFlags permission, CancellationToken ct = default)
        => channel.RemovePermissionAsync(Id, permission, isRole: true, ct);

    /// <summary>
    /// Denies a channel permission for this role.
    /// Example: await role.DenyChannelPermissionAsync(channel, PermissionFlags.SendMessages);
    /// </summary>
    public Task DenyChannelPermissionAsync(DiscordChannel channel, PermissionFlags permission, CancellationToken ct = default)
        => channel.DenyPermissionAsync(Id, permission, isRole: true, ct);

    /// <summary>
    /// Modifies channel permissions for this role.
    /// Example: await role.ModifyChannelPermissionsAsync(channel, allow: PermissionFlags.SendMessages | PermissionFlags.ViewChannel);
    /// </summary>
    public Task ModifyChannelPermissionsAsync(DiscordChannel channel, PermissionFlags? allow = null, PermissionFlags? deny = null, CancellationToken ct = default)
        => channel.ModifyPermissionsAsync(Id, allow, deny, isRole: true, ct);

    /// <summary>
    /// Deletes all channel permission overwrites for this role.
    /// Example: await role.DeleteChannelPermissionOverwriteAsync(channel);
    /// </summary>
    public Task DeleteChannelPermissionOverwriteAsync(DiscordChannel channel, CancellationToken ct = default)
        => channel.DeletePermissionOverwriteAsync(Id, ct);
}
