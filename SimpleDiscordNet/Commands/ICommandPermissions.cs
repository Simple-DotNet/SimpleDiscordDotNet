namespace SimpleDiscordNet.Commands;

/// <summary>
/// Interface for managing custom per-guild command permissions at runtime.
/// Allows registering custom permission rules that are checked before command execution.
/// Works alongside Discord-level default permissions ([RequirePermissions] attribute).
/// </summary>
public interface ICommandPermissions
{
    /// <summary>
    /// Registers a custom permission check for a specific command in a specific guild.
    /// The callback will be invoked before the command executes to determine if the user has permission.
    /// Multiple rules can be registered for the same command (all must pass).
    /// Example: bot.Permissions.RegisterGuildRule("123456789", "ban", ctx => ctx.Member?.Roles.Any(r => r.Id == "mod_role_id") ?? false);
    /// </summary>
    /// <param name="guildId">The guild ID where this rule applies</param>
    /// <param name="commandName">The slash command name (top-level command, not subcommand)</param>
    /// <param name="permissionCheck">A callback that returns true if the user has permission, false otherwise</param>
    void RegisterGuildRule(string guildId, string commandName, Func<InteractionContext, bool> permissionCheck);

    /// <summary>
    /// Removes all custom permission rules for a specific command in a specific guild.
    /// Example: bot.Permissions.ClearGuildRule("123456789", "ban");
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="commandName">The slash command name</param>
    void ClearGuildRule(string guildId, string commandName);

    /// <summary>
    /// Removes all custom permission rules for all commands in a specific guild.
    /// Example: bot.Permissions.ClearAllGuildRules("123456789");
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    void ClearAllGuildRules(string guildId);

    /// <summary>
    /// Checks if a user has permission to execute a command based on registered rules.
    /// Returns true if no rules are registered or all rules pass.
    /// This is called automatically by the framework before command execution.
    /// </summary>
    /// <param name="guildId">The guild ID (null for DM commands)</param>
    /// <param name="commandName">The slash command name</param>
    /// <param name="context">The interaction context containing user and member information</param>
    /// <returns>True if the user has permission, false otherwise</returns>
    bool CheckPermission(string? guildId, string commandName, InteractionContext context);
}
