using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Extensions;

/// <summary>
/// Extension methods for working with Discord permissions.
/// Makes it easier for beginners to check permissions.
/// </summary>
public static class PermissionExtensions
{
    extension(DiscordRole role)
    {
        /// <summary>
        /// Checks if a role has a specific permission.
        /// Example: if (role.HasPermission(PermissionFlags.Administrator)) { ... }
        /// </summary>
        public bool HasPermission(PermissionFlags permission)
        {
            ulong perms = role.Permissions;
            return (perms & (ulong)permission) != 0;
        }

        /// <summary>
        /// Checks if a role has administrator permission.
        /// Example: if (role.IsAdmin()) { ... }
        /// </summary>
        public bool IsAdmin()
            => role.HasPermission(PermissionFlags.Administrator);

        /// <summary>
        /// Checks if a role can manage messages.
        /// Example: if (role.CanManageMessages()) { ... }
        /// </summary>
        public bool CanManageMessages()
            => role.HasPermission(PermissionFlags.ManageMessages);

        /// <summary>
        /// Checks if a role can kick members.
        /// Example: if (role.CanKickMembers()) { ... }
        /// </summary>
        public bool CanKickMembers()
            => role.HasPermission(PermissionFlags.KickMembers);

        /// <summary>
        /// Checks if a role can ban members.
        /// Example: if (role.CanBanMembers()) { ... }
        /// </summary>
        public bool CanBanMembers()
            => role.HasPermission(PermissionFlags.BanMembers);
    }

    extension(DiscordMember member)
    {
        /// <summary>
        /// Gets the permission bits for a member from their interaction permissions.
        /// Example: ulong perms = member.GetPermissions();
        /// </summary>
        public ulong GetPermissions() => member.Permissions ?? 0UL;

        /// <summary>
        /// Checks if a member has a specific permission (from interaction context).
        /// Example: if (member.HasPermission(PermissionFlags.Administrator)) { ... }
        /// </summary>
        public bool HasPermission(PermissionFlags permission)
        {
            ulong perms = member.Permissions ?? 0UL;
            return (perms & (ulong)permission) != 0;
        }

        /// <summary>
        /// Checks if a member is an administrator.
        /// Example: if (member.IsAdmin()) { ... }
        /// </summary>
        public bool IsAdmin() => member.HasPermission(PermissionFlags.Administrator);
    }
}
