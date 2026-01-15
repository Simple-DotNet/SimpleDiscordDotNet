using System.Collections.Concurrent;
using SimpleDiscordNet.Logging;

namespace SimpleDiscordNet.Commands;

/// <summary>
/// Thread-safe service for managing and checking custom per-guild command permissions.
/// Stores permission rules in memory and evaluates them before command execution.
/// </summary>
internal sealed class CommandPermissionService : ICommandPermissions
{
    private readonly NativeLogger _logger;

    // guildId -> commandName -> list of permission checks
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, List<Func<InteractionContext, bool>>>> _guildRules = new(StringComparer.Ordinal);

    public CommandPermissionService(NativeLogger logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public void RegisterGuildRule(string guildId, string commandName, Func<InteractionContext, bool> permissionCheck)
    {
        ArgumentNullException.ThrowIfNull(guildId);
        ArgumentNullException.ThrowIfNull(commandName);
        ArgumentNullException.ThrowIfNull(permissionCheck);

        var commandRules = _guildRules.GetOrAdd(guildId, _ => new ConcurrentDictionary<string, List<Func<InteractionContext, bool>>>(StringComparer.Ordinal));

        lock (commandRules)
        {
            if (!commandRules.TryGetValue(commandName, out List<Func<InteractionContext, bool>>? rules))
            {
                rules = [];
                commandRules[commandName] = rules;
            }
            rules.Add(permissionCheck);
        }

        _logger.Log(LogLevel.Debug, $"Registered custom permission rule for command '{commandName}' in guild {guildId}");
    }

    /// <inheritdoc />
    public void ClearGuildRule(string guildId, string commandName)
    {
        ArgumentNullException.ThrowIfNull(guildId);
        ArgumentNullException.ThrowIfNull(commandName);

        if (_guildRules.TryGetValue(guildId, out var commandRules))
        {
            lock (commandRules)
            {
                commandRules.TryRemove(commandName, out _);
            }
            _logger.Log(LogLevel.Debug, $"Cleared custom permission rules for command '{commandName}' in guild {guildId}");
        }
    }

    /// <inheritdoc />
    public void ClearAllGuildRules(string guildId)
    {
        ArgumentNullException.ThrowIfNull(guildId);

        _guildRules.TryRemove(guildId, out _);
        _logger.Log(LogLevel.Debug, $"Cleared all custom permission rules for guild {guildId}");
    }

    /// <inheritdoc />
    public bool CheckPermission(string? guildId, string commandName, InteractionContext context)
    {
        // DM commands always pass (no guild-specific rules)
        if (guildId is null)
            return true;

        // No rules registered = allow
        if (!_guildRules.TryGetValue(guildId, out var commandRules))
            return true;

        // No rules for this command = allow
        if (!commandRules.TryGetValue(commandName, out List<Func<InteractionContext, bool>>? rules))
            return true;

        // All rules must pass
        lock (rules)
        {
            foreach (var rule in rules)
            {
                try
                {
                    if (!rule(context))
                    {
                        _logger.Log(LogLevel.Debug, $"Permission denied for command '{commandName}' in guild {guildId} for user {context.User.Id}");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Error, $"Error evaluating permission rule for command '{commandName}' in guild {guildId}: {ex.Message}", ex);
                    // On error, deny permission for safety
                    return false;
                }
            }
        }

        return true;
    }
}
