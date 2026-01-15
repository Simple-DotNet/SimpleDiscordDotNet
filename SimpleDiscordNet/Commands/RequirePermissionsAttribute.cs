using SimpleDiscordNet.Primitives;

namespace SimpleDiscordNet.Commands;

/// <summary>
/// Specifies the default Discord permissions required to use a slash command.
/// Sets the default_member_permissions field in the Discord API, which controls command visibility.
/// Commands with this attribute will only be visible to users who have ALL of the specified permissions.
/// Server administrators can override these defaults with guild-specific permission settings.
/// Example: [RequirePermissions(PermissionFlags.BanMembers | PermissionFlags.KickMembers)]
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class RequirePermissionsAttribute : Attribute
{
    /// <summary>
    /// The Discord permissions required to use this command (bitwise OR combination).
    /// </summary>
    public PermissionFlags Permissions { get; }

    /// <summary>
    /// Creates a new RequirePermissionsAttribute with the specified permission flags.
    /// </summary>
    /// <param name="permissions">The Discord permissions required (can use bitwise OR: PermissionFlags.BanMembers | PermissionFlags.KickMembers)</param>
    public RequirePermissionsAttribute(PermissionFlags permissions)
    {
        Permissions = permissions;
    }
}
