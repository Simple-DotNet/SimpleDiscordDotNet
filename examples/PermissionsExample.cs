using SimpleDiscordNet;
using SimpleDiscordNet.Commands;
using SimpleDiscordNet.Primitives;

namespace Examples;

/// <summary>
/// Example demonstrating the dual-layer permission system in SimpleDiscordDotNet.
///
/// Layer 1: Discord-level default permissions (compile-time via [RequirePermissions] attribute)
/// - Sets default_member_permissions in Discord API
/// - Discord enforces this before showing the command to users
/// - Static, set once during command registration
///
/// Layer 2: Runtime custom permissions (per-guild via bot.Permissions API)
/// - Custom rules registered at runtime
/// - Checked by framework before command execution
/// - Dynamic, can be changed per-guild without redeploying
/// </summary>
public sealed class PermissionsExample
{
    // Example 1: Discord-level permissions only
    // Command only visible to users with BanMembers permission
    [RequirePermissions(PermissionFlags.BanMembers)]
    [SlashCommand("ban", "Ban a user from the server")]
    public async Task BanCommand(InteractionContext ctx, DiscordUser user, string? reason = null)
    {
        await ctx.RespondAsync($"Banning {user.Username}... (reason: {reason ?? "No reason provided"})");
        // Implementation...
    }

    // Example 2: Discord-level permissions with multiple flags
    // Command visible to users with BOTH KickMembers AND ManageMessages
    [RequirePermissions(PermissionFlags.KickMembers | PermissionFlags.ManageMessages)]
    [SlashCommand("kick", "Kick a user from the server")]
    public async Task KickCommand(InteractionContext ctx, DiscordUser user, string? reason = null)
    {
        await ctx.RespondAsync($"Kicking {user.Username}... (reason: {reason ?? "No reason provided"})");
        // Implementation...
    }

    // Example 3: No attribute = no Discord-level restrictions
    // Command visible to everyone, but can be restricted via runtime rules
    [SlashCommand("info", "Get server information")]
    public async Task InfoCommand(InteractionContext ctx)
    {
        await ctx.RespondAsync($"Server: {ctx.Guild?.Name ?? "Unknown"}");
    }

    // Example 4: Administrator-only command
    [RequirePermissions(PermissionFlags.Administrator)]
    [SlashCommand("config", "Configure bot settings")]
    public async Task ConfigCommand(InteractionContext ctx)
    {
        await ctx.RespondAsync("Opening configuration panel...");
    }
}

/// <summary>
/// Example of setting up runtime per-guild permissions.
/// </summary>
public class Program
{
    public static async Task Main()
    {
        var bot = DiscordBot.NewBuilder()
            .WithToken(Environment.GetEnvironmentVariable("DISCORD_TOKEN")!)
            .WithIntents(DiscordIntents.Guilds | DiscordIntents.GuildMessages)
            .Build();

        await bot.StartAsync();

        // ===== Runtime Permission Examples =====

        // Example 1: Simple role check
        // Only users with the "Moderator" role can use the ban command in guild "123456789"
        bot.Permissions.RegisterGuildRule("123456789", "ban", ctx =>
            ctx.Member?.Roles.Any(r => r.Name == "Moderator") ?? false);

        // Example 2: Multiple roles (OR logic)
        // Users with either "Admin" OR "Senior Moderator" role can use kick command
        bot.Permissions.RegisterGuildRule("123456789", "kick", ctx =>
            ctx.Member?.Roles.Any(r => r.Name == "Admin" || r.Name == "Senior Moderator") ?? false);

        // Example 3: Role ID check (more reliable than name)
        bot.Permissions.RegisterGuildRule("123456789", "config", ctx =>
            ctx.Member?.Roles.Any(r => r.Id == "987654321") ?? false);

        // Example 4: Specific users (allowlist)
        var allowedUsers = new[] { "111111111", "222222222", "333333333" };
        bot.Permissions.RegisterGuildRule("123456789", "info", ctx =>
            allowedUsers.Contains(ctx.User.Id));

        // Example 5: Complex logic (multiple conditions)
        bot.Permissions.RegisterGuildRule("123456789", "ban", ctx =>
        {
            // Must have moderator role AND account older than 30 days
            bool hasModerator = ctx.Member?.Roles.Any(r => r.Name == "Moderator") ?? false;
            bool accountOldEnough = (DateTimeOffset.UtcNow - ctx.User.CreatedAt).TotalDays >= 30;
            return hasModerator && accountOldEnough;
        });

        // Example 6: Load permissions from database
        var guildConfigs = await LoadGuildConfigsFromDatabase();
        foreach (var config in guildConfigs)
        {
            foreach (var cmd in config.RestrictedCommands)
            {
                // Capture local variables for closure
                var allowedRoleIds = cmd.AllowedRoleIds.ToHashSet();
                bot.Permissions.RegisterGuildRule(config.GuildId, cmd.CommandName, ctx =>
                    ctx.Member?.Roles.Any(r => allowedRoleIds.Contains(r.Id)) ?? false);
            }
        }

        // Example 7: Clear permissions for a command
        bot.Permissions.ClearGuildRule("123456789", "ban");

        // Example 8: Clear all permissions for a guild
        bot.Permissions.ClearAllGuildRules("123456789");

        await Task.Delay(Timeout.Infinite);
    }

    private static async Task<List<GuildConfig>> LoadGuildConfigsFromDatabase()
    {
        // Placeholder - implement your own database loading
        await Task.CompletedTask;
        return new List<GuildConfig>();
    }
}

// Example configuration classes for database storage
public class GuildConfig
{
    public required string GuildId { get; set; }
    public List<RestrictedCommand> RestrictedCommands { get; set; } = new();
}

public class RestrictedCommand
{
    public required string CommandName { get; set; }
    public List<string> AllowedRoleIds { get; set; } = new();
}
