using System.Collections.Concurrent;
using System.Globalization;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Models;
using SimpleDiscordNet.Rest;

namespace SimpleDiscordNet.Commands;

/// <summary>
/// A check that runs before a handler. Returns the message to show the user, or null if the check passed.
/// </summary>
public delegate ValueTask<string?> CommandCheck(InteractionContext ctx, CancellationToken ct);

/// <summary>
/// The checks behind attributes such as [Cooldown]. Generated code creates these.
/// </summary>
public static class CommandChecks
{
    private const int CooldownCleanupSize = 10_000;
    private const string ReadOnlyTeamRole = "read_only";

    private static readonly Lock CooldownGate = new();
    private static readonly Dictionary<string, DateTimeOffset> CooldownExpiry = [];
    private static readonly ConcurrentDictionary<string, Task<HashSet<ulong>>> OwnersByApplication = new();

    internal static TimeProvider Time { get; set; } = TimeProvider.System;

    /// <summary>
    /// Runs checks in order and tells the user about the first failure.
    /// </summary>
    /// <returns>True if the handler should run.</returns>
    internal static async ValueTask<bool> PassAsync(IReadOnlyList<CommandCheck>? checks, InteractionContext ctx, CancellationToken ct)
    {
        if (checks is null) return true;

        foreach (CommandCheck check in checks)
        {
            string? failure = await check(ctx, ct).ConfigureAwait(false);
            if (failure is null) continue;

            await ctx.RespondAsync(failure, null, true, ct: ct).ConfigureAwait(false);
            return false;
        }

        return true;
    }

    public static CommandCheck Cooldown(string key, double seconds, CooldownScope scope, string? message = null)
    {
        return (ctx, _) =>
        {
            string target = scope switch
            {
                CooldownScope.User => ctx.User?.Id.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                CooldownScope.Channel => ctx.ChannelId ?? string.Empty,
                CooldownScope.Guild => ctx.GuildId ?? ctx.ChannelId ?? string.Empty,
                _ => string.Empty
            };
            string entry = $"{key}|{(int)scope}|{target}";
            DateTimeOffset now = Time.GetUtcNow();

            lock (CooldownGate)
            {
                if (CooldownExpiry.TryGetValue(entry, out DateTimeOffset expiry) && expiry > now)
                {
                    int wait = (int)Math.Ceiling((expiry - now).TotalSeconds);
                    return ValueTask.FromResult<string?>(message ?? $"⏳ Try again in {wait} {(wait == 1 ? "second" : "seconds")}.");
                }

                RemoveExpiredCooldowns(now);
                CooldownExpiry[entry] = now.AddSeconds(seconds);
            }

            return ValueTask.FromResult<string?>(null);
        };
    }

    private static void RemoveExpiredCooldowns(DateTimeOffset now)
    {
        if (CooldownExpiry.Count < CooldownCleanupSize) return;

        foreach (KeyValuePair<string, DateTimeOffset> cooldown in CooldownExpiry)
        {
            if (cooldown.Value <= now) CooldownExpiry.Remove(cooldown.Key);
        }
    }

    public static CommandCheck RequireRole(string[] roles, string? message = null)
    {
        return (ctx, _) =>
        {
            if (ctx.Member is not { } member)
                return ValueTask.FromResult<string?>(message ?? "❌ This only works in a server.");

            foreach (string role in roles)
            {
                if (HasRole(ctx, member, role))
                    return ValueTask.FromResult<string?>(null);
            }

            string names = string.Join(", ", roles);
            return ValueTask.FromResult<string?>(message ?? $"❌ You need one of these roles: {names}.");
        };
    }

    private static bool HasRole(InteractionContext ctx, DiscordMember member, string role)
    {
        if (ulong.TryParse(role, NumberStyles.None, CultureInfo.InvariantCulture, out ulong id))
            return member.HasRole(id);

        DiscordRole? named = ctx.Guild?.Roles?.FirstOrDefault(r => r.Name.Equals(role, StringComparison.OrdinalIgnoreCase));
        return named is not null && member.HasRole(named.Id);
    }

    public static CommandCheck RequireBotPermissions(ulong permissions, string? message = null)
    {
        return (ctx, _) =>
        {
            // Discord sends this with every interaction. If it's ever absent, let Discord decide
            if (ctx.BotPermissions is not { } granted || (granted & (ulong)PermissionFlags.Administrator) != 0)
                return ValueTask.FromResult<string?>(null);

            ulong missing = permissions & ~granted;
            return ValueTask.FromResult(missing == 0 ? null : message ?? $"❌ I need these permissions: {(PermissionFlags)missing}.");
        };
    }

    public static CommandCheck RequireOwner(string? message = null)
    {
        return async (ctx, ct) =>
        {
            HashSet<ulong> owners = await GetOwnersAsync(ctx.ApplicationId, ctx.Rest, ct).ConfigureAwait(false);
            bool isOwner = ctx.User is { } user && owners.Contains(user.Id);
            return isOwner ? null : message ?? "❌ Only the bot's owner can use this.";
        };
    }

    private static async Task<HashSet<ulong>> GetOwnersAsync(string applicationId, RestClient rest, CancellationToken ct)
    {
        Task<HashSet<ulong>> lookup = OwnersByApplication.GetOrAdd(applicationId, _ => LoadOwnersAsync(rest, ct));
        try
        {
            return await lookup.ConfigureAwait(false);
        }
        catch
        {
            // A failed lookup must not stick, or the check would fail until a restart
            OwnersByApplication.TryRemove(applicationId, out _);
            throw;
        }
    }

    private static async Task<HashSet<ulong>> LoadOwnersAsync(RestClient rest, CancellationToken ct)
    {
        ApplicationInfo app = await rest.GetApplicationAsync(ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Couldn't read the application's owner from Discord.");

        HashSet<ulong> owners = [];
        if (app.Team is { } team)
        {
            owners.Add(team.Owner_User_Id);
            foreach (ApplicationTeamMember member in team.Members)
            {
                if (member.Role != ReadOnlyTeamRole) owners.Add(member.User.Id);
            }
        }
        else if (app.Owner is { } owner)
        {
            owners.Add(owner.Id);
        }

        return owners;
    }

    internal static void ResetForTests()
    {
        lock (CooldownGate) CooldownExpiry.Clear();
        OwnersByApplication.Clear();
    }
}
