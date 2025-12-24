namespace SimpleDiscordNet.Primitives;

/// <summary>
/// Builder for creating Discord permission overwrite objects for channel management.
/// Supports both role and member permission overwrites.
/// </summary>
public sealed class PermissionBuilder
{
    private PermissionFlags _allow;
    private PermissionFlags _deny;
    private readonly string _id;
    private readonly int _type; // 0 = role, 1 = member

    private PermissionBuilder(string id, int type)
    {
        _id = id;
        _type = type;
    }

    /// <summary>Creates a permission builder for a role.</summary>
    public static PermissionBuilder ForRole(string roleId) => new(roleId, 0);

    /// <summary>Creates a permission builder for a role.</summary>
    public static PermissionBuilder ForRole(ulong roleId) => new(roleId.ToString(), 0);

    /// <summary>Creates a permission builder for a member.</summary>
    public static PermissionBuilder ForMember(string memberId) => new(memberId, 1);

    /// <summary>Creates a permission builder for a member.</summary>
    public static PermissionBuilder ForMember(ulong memberId) => new(memberId.ToString(), 1);

    /// <summary>Allows specific permissions.</summary>
    public PermissionBuilder Allow(PermissionFlags permissions)
    {
        _allow |= permissions;
        _deny &= ~permissions; // Remove from deny if present
        return this;
    }

    /// <summary>Denies specific permissions.</summary>
    public PermissionBuilder Deny(PermissionFlags permissions)
    {
        _deny |= permissions;
        _allow &= ~permissions; // Remove from allow if present
        return this;
    }

    /// <summary>Clears specific permissions (neutral).</summary>
    public PermissionBuilder Clear(PermissionFlags permissions)
    {
        _allow &= ~permissions;
        _deny &= ~permissions;
        return this;
    }

    /// <summary>Gets the allow permissions as ulong.</summary>
    public ulong GetAllowBits() => (ulong)_allow;

    /// <summary>Gets the deny permissions as ulong.</summary>
    public ulong GetDenyBits() => (ulong)_deny;

    /// <summary>Gets the target ID (role or member).</summary>
    public string GetId() => _id;

    /// <summary>Gets the type (0 = role, 1 = member).</summary>
    public int GetPermissionType() => _type;

    /// <summary>Builds the permission overwrite object for Discord API.</summary>
    public object Build() => new
    {
        id = _id,
        type = _type,
        allow = ((ulong)_allow).ToString(),
        deny = ((ulong)_deny).ToString()
    };

    /// <summary>Quick builder for common "read-only" channel permissions.</summary>
    public static PermissionBuilder ReadOnlyRole(string roleId) =>
        ForRole(roleId)
            .Allow(PermissionFlags.ViewChannel | PermissionFlags.ReadMessageHistory)
            .Deny(PermissionFlags.SendMessages | PermissionFlags.AddReactions);

    /// <summary>Quick builder for common "read-only" channel permissions.</summary>
    public static PermissionBuilder ReadOnlyRole(ulong roleId) =>
        ForRole(roleId)
            .Allow(PermissionFlags.ViewChannel | PermissionFlags.ReadMessageHistory)
            .Deny(PermissionFlags.SendMessages | PermissionFlags.AddReactions);

    /// <summary>Quick builder for common "moderator" channel permissions.</summary>
    public static PermissionBuilder ModeratorRole(string roleId) =>
        ForRole(roleId)
            .Allow(PermissionFlags.ViewChannel |
                   PermissionFlags.SendMessages |
                   PermissionFlags.ManageMessages |
                   PermissionFlags.ManageThreads |
                   PermissionFlags.ReadMessageHistory);

    /// <summary>Quick builder for common "moderator" channel permissions.</summary>
    public static PermissionBuilder ModeratorRole(ulong roleId) =>
        ForRole(roleId)
            .Allow(PermissionFlags.ViewChannel |
                   PermissionFlags.SendMessages |
                   PermissionFlags.ManageMessages |
                   PermissionFlags.ManageThreads |
                   PermissionFlags.ReadMessageHistory);

    /// <summary>Quick builder to deny all channel access.</summary>
    public static PermissionBuilder DenyAllRole(string roleId) =>
        ForRole(roleId).Deny(PermissionFlags.ViewChannel);

    /// <summary>Quick builder to deny all channel access.</summary>
    public static PermissionBuilder DenyAllRole(ulong roleId) =>
        ForRole(roleId).Deny(PermissionFlags.ViewChannel);
}
