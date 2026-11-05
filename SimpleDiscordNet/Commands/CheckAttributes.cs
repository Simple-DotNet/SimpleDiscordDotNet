namespace SimpleDiscordNet.Commands;

/// <summary>
/// Who shares a <see cref="CooldownAttribute"/>.
/// </summary>
public enum CooldownScope
{
    /// <summary>Each user has their own cooldown.</summary>
    User,

    /// <summary>Everyone in a channel shares one cooldown.</summary>
    Channel,

    /// <summary>Everyone in a server shares one cooldown.</summary>
    Guild,

    /// <summary>Everyone shares one cooldown.</summary>
    Global
}

/// <summary>
/// Base class for checks that run before a handler. If a check fails, the handler doesn't run and
/// the user gets a private message saying why. Use <see cref="Message"/> to change that text.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public abstract class CheckAttribute : Attribute
{
    /// <summary>The message to show when this check fails. Leave unset for the default.</summary>
    public string? Message { get; set; }
}

/// <summary>
/// Limits how often a command, button or other handler can be used.
/// Example: [Cooldown(30)] or [Cooldown(60, Scope = CooldownScope.Guild)]
/// </summary>
public sealed class CooldownAttribute(double seconds) : CheckAttribute
{
    public double Seconds { get; } = seconds;

    /// <summary>Who shares the cooldown. Defaults to each user having their own.</summary>
    public CooldownScope Scope { get; set; } = CooldownScope.User;
}

/// <summary>
/// Requires the user to have at least one of these roles, given by name or ID.
/// Example: [RequireRole("Moderator", "Admin")]
/// </summary>
public sealed class RequireRoleAttribute(params string[] roles) : CheckAttribute
{
    public string[] Roles { get; } = roles;
}

/// <summary>
/// Requires the bot to have these permissions where the handler is used.
/// Example: [RequireBotPermissions(PermissionFlags.BanMembers)]
/// </summary>
public sealed class RequireBotPermissionsAttribute(PermissionFlags permissions) : CheckAttribute
{
    public PermissionFlags Permissions { get; } = permissions;
}

/// <summary>
/// Limits a handler to the bot's owner. For an app owned by a team, this means the team's owner,
/// admins and developers. Read-only team members aren't included.
/// </summary>
public sealed class RequireOwnerAttribute : CheckAttribute;
