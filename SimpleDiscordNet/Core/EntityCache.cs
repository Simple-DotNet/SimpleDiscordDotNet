using System.Collections.Concurrent;
using SimpleDiscordNet.Collections;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Sharding;

namespace SimpleDiscordNet.Core;

/// <summary>
/// Thread-safe in-memory cache of Discord entities known to the bot.
/// Supports both snapshot-based access (backward compatible) and live observable collections for UI binding.
/// </summary>
internal sealed class EntityCache
{
    private readonly ObservableConcurrentDictionary<ulong, DiscordGuild> _guilds;
    private readonly ConcurrentDictionary<ulong, ObservableConcurrentList<DiscordChannel>> _channelsByGuild = new();
    private readonly ConcurrentDictionary<ulong, ObservableConcurrentList<DiscordMember>> _membersByGuild = new();
    private SynchronizationContext? _synchronizationContext;

    public EntityCache()
    {
        _guilds = new ObservableConcurrentDictionary<ulong, DiscordGuild>();
    }

    /// <summary>
    /// Sets the synchronization context for marshaling collection change notifications to the UI thread.
    /// Must be called before the bot connects if UI thread marshaling is desired.
    /// </summary>
    public void SetSynchronizationContext(SynchronizationContext? synchronizationContext)
    {
        _synchronizationContext = synchronizationContext;
    }

    /// <summary>
    /// Gets the live observable dictionary of guilds. Can be used for UI binding.
    /// Changes to this collection automatically raise change notifications.
    /// </summary>
    public ObservableConcurrentDictionary<ulong, DiscordGuild> LiveGuilds => _guilds;

    /// <summary>
    /// Gets all live channel collections flattened into a single enumerable.
    /// Note: For UI binding to a specific guild's channels, use GetLiveChannels(guildId).
    /// </summary>
    public IEnumerable<DiscordChannel> LiveChannels
    {
        get
        {
            foreach (var kvp in _channelsByGuild)
            {
                foreach (var channel in kvp.Value.ToArray())
                {
                    yield return channel;
                }
            }
        }
    }

    /// <summary>
    /// Gets all live member collections flattened into a single enumerable.
    /// Note: For UI binding to a specific guild's members, use GetLiveMembers(guildId).
    /// </summary>
    public IEnumerable<DiscordMember> LiveMembers
    {
        get
        {
            foreach (var kvp in _membersByGuild)
            {
                foreach (var member in kvp.Value.ToArray())
                {
                    yield return member;
                }
            }
        }
    }

    /// <summary>
    /// Gets the live observable list of channels for a specific guild.
    /// Can be used for UI binding. Returns null if guild has no channels cached.
    /// </summary>
    public ObservableConcurrentList<DiscordChannel>? GetLiveChannels(ulong guildId)
    {
        return _channelsByGuild.TryGetValue(guildId, out var list) ? list : null;
    }

    /// <summary>
    /// Gets the live observable list of members for a specific guild.
    /// Can be used for UI binding. Returns null if guild has no members cached.
    /// </summary>
    public ObservableConcurrentList<DiscordMember>? GetLiveMembers(ulong guildId)
    {
        return _membersByGuild.TryGetValue(guildId, out var list) ? list : null;
    }

    public void ReplaceGuilds(IEnumerable<DiscordGuild> guilds)
    {
        _guilds.ReplaceAll(guilds.Select(g => new KeyValuePair<ulong, DiscordGuild>(g.Id, g)));
    }

    public void SetChannels(ulong guildId, IEnumerable<DiscordChannel> channels)
    {
        if (!_guilds.TryGetValue(guildId, out DiscordGuild? guild))
        {
            // Guild not in cache yet - store channels as-is (Guild will be set later via UpsertChannel)
            var list = new ObservableConcurrentList<DiscordChannel>(_synchronizationContext);
            list.AddRange(channels);
            _channelsByGuild[guildId] = list;
            return;
        }

        // Ensure all channels have the Guild property set
        List<DiscordChannel> channelList = [];
        foreach (DiscordChannel channel in channels)
        {
            if (channel.Guild?.Id == guildId)
            {
                // Guild already set correctly
                channelList.Add(channel);
            }
            else
            {
                // Set Guild property - channels are mutable so we can update directly
                channel.Guild = guild;
                channelList.Add(channel);
            }
        }

        // Use batch operation to avoid multiple notifications
        var observableList = new ObservableConcurrentList<DiscordChannel>(_synchronizationContext);
        observableList.AddRange(channelList);
        _channelsByGuild[guildId] = observableList;
    }

    public void SetMembers(ulong guildId, IEnumerable<DiscordMember> members)
    {
        if (!_guilds.TryGetValue(guildId, out DiscordGuild guild))
        {
            // Guild not in cache yet - store members as-is (Guild will be set later via UpsertMember)
            var list = new ObservableConcurrentList<DiscordMember>(_synchronizationContext);
            list.AddRange(members);
            _membersByGuild[guildId] = list;
            return;
        }

        // Ensure all members have the Guild property set
        List<DiscordMember> memberList = [];
        foreach (DiscordMember member in members)
        {
            if (member.Guild.Id == guildId)
            {
                // Guild already set correctly
                memberList.Add(member);
            }
            else
            {
                // Need to set Guild property - create new instance with guild
                DiscordMember memberWithGuild = new()
                {
                    User = member.User,
                    Guild = guild,
                    Nick = member.Nick,
                    Roles = member.Roles,
                    Avatar = member.Avatar,
                    Joined_At = member.Joined_At,
                    Premium_Since = member.Premium_Since,
                    Deaf = member.Deaf,
                    Mute = member.Mute,
                    Flags = member.Flags,
                    Pending = member.Pending,
                    Permissions = member.Permissions,
                    Communication_Disabled_Until = member.Communication_Disabled_Until
                };
                memberList.Add(memberWithGuild);
            }
        }

        // Use batch operation to avoid multiple notifications
        var observableList = new ObservableConcurrentList<DiscordMember>(_synchronizationContext);
        observableList.AddRange(memberList);
        _membersByGuild[guildId] = observableList;
    }

    public IReadOnlyList<DiscordGuild> SnapshotGuilds() => _guilds.Values.OrderBy(g => g.Id).ToArray();

    public IReadOnlyList<DiscordChannel> SnapshotChannels()
    {
        List<DiscordChannel> list = new(1024);
        foreach ((ulong gid, DiscordGuild guild) in _guilds)
        {
            if (!_channelsByGuild.TryGetValue(gid, out ObservableConcurrentList<DiscordChannel>? chs)) continue;
            var snapshot = chs.ToArray();
            list.EnsureCapacity(list.Count + snapshot.Length);
            for (int index = snapshot.Length - 1; index >= 0; index--)
            {
                list.Add(snapshot[index]);
            }
        }
        return list;
    }

    public IReadOnlyList<DiscordMember> SnapshotMembers()
    {
        List<DiscordMember> list = new(2048);
        foreach ((ulong gid, DiscordGuild guild) in _guilds)
        {
            if (!_membersByGuild.TryGetValue(gid, out ObservableConcurrentList<DiscordMember>? members)) continue;
            var snapshot = members.ToArray();
            list.EnsureCapacity(list.Count + snapshot.Length);
            for (int index = snapshot.Length - 1; index >= 0; index--)
            {
                list.Add(snapshot[index]);
            }
        }
        return list;
    }

    public IReadOnlyList<DiscordUser> SnapshotUsers()
    {
        // Build mapping of user ID to guilds they're in
        Dictionary<ulong, List<DiscordGuild>> userGuilds = new();
        foreach ((ulong gid, DiscordGuild guild) in _guilds)
        {
            if (!_membersByGuild.TryGetValue(gid, out ObservableConcurrentList<DiscordMember>? members)) continue;
            var memberSnapshot = members.ToArray();
            foreach (DiscordMember member in memberSnapshot)
            {
                if (!userGuilds.TryGetValue(member.User.Id, out List<DiscordGuild>? guilds))
                {
                    guilds = new List<DiscordGuild>();
                    userGuilds[member.User.Id] = guilds;
                }
                guilds.Add(guild);
            }
        }

        // Update each user's Guilds array and collect distinct users
        Dictionary<ulong, DiscordUser> distinctUsers = new();
        foreach ((ulong userId, List<DiscordGuild> guilds) in userGuilds)
        {
            // Find the user from any member (they all have the same User object reference potentially)
            foreach (ObservableConcurrentList<DiscordMember> members in _membersByGuild.Values)
            {
                DiscordMember? member = members.FirstOrDefault(m => m.User.Id == userId);
                if (member != null)
                {
                    member.User.Guilds = guilds.ToArray();
                    distinctUsers[userId] = member.User;
                    break;
                }
            }
        }

        return distinctUsers.Values.ToArray();
    }

    public IReadOnlyList<DiscordRole> SnapshotRoles()
    {
        List<DiscordRole> list = new(1024);
        foreach ((ulong gid, DiscordGuild guild) in _guilds)
        {
            if (guild.Roles == null) continue;
            list.EnsureCapacity(list.Count + guild.Roles.Length);
            foreach (var role in guild.Roles)
            {
                list.Add(role);
            }
        }
        return list;
    }

    // --- Shard-aware snapshot methods ---

    /// <summary>
    /// Returns guilds that belong to a specific shard.
    /// Example: var guilds = cache.SnapshotGuildsForShard(0, 4);
    /// </summary>
    public IReadOnlyList<DiscordGuild> SnapshotGuildsForShard(int shardId, int totalShards)
    {
        return _guilds.Values
            .Where(g => ShardCalculator.CalculateShardId(g.Id.ToString().AsSpan(), totalShards) == shardId)
            .OrderBy(g => g.Id)
            .ToArray();
    }

    /// <summary>
    /// Returns channels that belong to guilds in a specific shard.
    /// Example: var channels = cache.SnapshotChannelsForShard(0, 4);
    /// </summary>
    public IReadOnlyList<DiscordChannel> SnapshotChannelsForShard(int shardId, int totalShards)
    {
        List<DiscordChannel> list = new(1024);
        foreach ((ulong gid, DiscordGuild guild) in _guilds)
        {
            if (ShardCalculator.CalculateShardId(gid.ToString().AsSpan(), totalShards) != shardId)
                continue;

            if (!_channelsByGuild.TryGetValue(gid, out ObservableConcurrentList<DiscordChannel>? chs)) continue;
            var snapshot = chs.ToArray();
            list.EnsureCapacity(list.Count + snapshot.Length);
            foreach (DiscordChannel c in snapshot)
            {
                list.Add(c);
            }
        }
        return list;
    }

    /// <summary>
    /// Returns members that belong to guilds in a specific shard.
    /// Example: var members = cache.SnapshotMembersForShard(0, 4);
    /// </summary>
    public IReadOnlyList<DiscordMember> SnapshotMembersForShard(int shardId, int totalShards)
    {
        List<DiscordMember> list = new(2048);
        foreach ((ulong gid, DiscordGuild guild) in _guilds)
        {
            if (ShardCalculator.CalculateShardId(gid.ToString().AsSpan(), totalShards) != shardId)
                continue;

            if (!_membersByGuild.TryGetValue(gid, out ObservableConcurrentList<DiscordMember>? members)) continue;
            var snapshot = members.ToArray();
            list.EnsureCapacity(list.Count + snapshot.Length);
            foreach (DiscordMember member in snapshot)
            {
                list.Add(member);
            }
        }
        return list;
    }

    /// <summary>
    /// Returns roles that belong to guilds in a specific shard.
    /// Example: var roles = cache.SnapshotRolesForShard(0, 4);
    /// </summary>
    public IReadOnlyList<DiscordRole> SnapshotRolesForShard(int shardId, int totalShards)
    {
        List<DiscordRole> list = new(1024);
        foreach ((ulong gid, DiscordGuild guild) in _guilds)
        {
            if (ShardCalculator.CalculateShardId(gid.ToString().AsSpan(), totalShards) != shardId)
                continue;

            if (guild.Roles == null) continue;
            list.EnsureCapacity(list.Count + guild.Roles.Length);
            foreach (DiscordRole role in guild.Roles)
            {
                list.Add(role);
            }
        }
        return list;
    }

    // --- Incremental mutation helpers for gateway events ---

    public void UpsertGuild(DiscordGuild guild)
    {
        _guilds[guild.Id] = guild;
    }

    public void RemoveGuild(ulong guildId)
    {
        _guilds.TryRemove(guildId, out _);
        _channelsByGuild.TryRemove(guildId, out _);
        _membersByGuild.TryRemove(guildId, out _);
    }

    public void UpsertChannel(ulong guildId, DiscordChannel channel)
    {
        // Ensure channel has Guild property set
        if (channel.Guild?.Id != guildId && _guilds.TryGetValue(guildId, out DiscordGuild? guild))
        {
            channel.Guild = guild;
        }

        ObservableConcurrentList<DiscordChannel> list = _channelsByGuild.GetOrAdd(guildId, _ => new ObservableConcurrentList<DiscordChannel>(_synchronizationContext));

        // Update existing or add new
        if (!list.Update(c => c.Id == channel.Id, channel))
        {
            list.Add(channel);
        }
    }

    public void RemoveChannel(ulong guildId, ulong channelId)
    {
        if (_channelsByGuild.TryGetValue(guildId, out ObservableConcurrentList<DiscordChannel>? list))
        {
            int idx = list.FindIndex(c => c.Id == channelId);
            if (idx >= 0) list.RemoveAt(idx);
        }
    }

    public void UpsertMember(ulong guildId, DiscordMember member)
    {
        // Ensure member has Guild property set
        DiscordMember memberWithGuild = member;
        if (member.Guild.Id != guildId && _guilds.TryGetValue(guildId, out DiscordGuild guild))
        {
            // Need to set Guild property - create new instance
            memberWithGuild = new()
            {
                User = member.User,
                Guild = guild,
                Nick = member.Nick,
                Roles = member.Roles,
                Avatar = member.Avatar,
                Joined_At = member.Joined_At,
                Premium_Since = member.Premium_Since,
                Deaf = member.Deaf,
                Mute = member.Mute,
                Flags = member.Flags,
                Pending = member.Pending,
                Permissions = member.Permissions,
                Communication_Disabled_Until = member.Communication_Disabled_Until
            };
        }

        ObservableConcurrentList<DiscordMember> list = _membersByGuild.GetOrAdd(guildId, _ => new ObservableConcurrentList<DiscordMember>(_synchronizationContext));

        // Update existing or add new
        if (!list.Update(m => m.User.Id == memberWithGuild.User.Id, memberWithGuild))
        {
            list.Add(memberWithGuild);
        }
    }

    public void RemoveMember(ulong guildId, ulong userId)
    {
        if (!_membersByGuild.TryGetValue(guildId, out ObservableConcurrentList<DiscordMember>? list)) return;
        int idx = list.FindIndex(m => m.User.Id == userId);
        if (idx >= 0) list.RemoveAt(idx);
    }

    public void UpsertRole(ulong guildId, DiscordRole role)
    {
        if (!_guilds.TryGetValue(guildId, out DiscordGuild guild)) return;

        // Ensure role has Guild property set
        DiscordRole roleWithGuild = role;
        if (role.Guild.Id != guildId)
        {
            // Need to set Guild property - create new instance
            roleWithGuild = new()
            {
                Id = role.Id,
                Name = role.Name,
                Guild = guild,
                Color = role.Color,
                Position = role.Position,
                Permissions = role.Permissions
            };
        }

        DiscordRole[] currentRoles = guild.Roles ?? [];
        int idx = Array.FindIndex(currentRoles, r => r.Id == roleWithGuild.Id);

        DiscordRole[] newRoles;
        if (idx >= 0)
        {
            // Update existing role
            newRoles = new DiscordRole[currentRoles.Length];
            currentRoles.AsSpan().CopyTo(newRoles.AsSpan());
            newRoles[idx] = roleWithGuild;
        }
        else
        {
            // Add new role
            newRoles = new DiscordRole[currentRoles.Length + 1];
            currentRoles.AsSpan().CopyTo(newRoles.AsSpan());
            newRoles[^1] = roleWithGuild;
        }

        // Update guild with new roles array
        guild.Roles = newRoles;
    }

    public void RemoveRole(ulong guildId, ulong roleId)
    {
        if (!_guilds.TryGetValue(guildId, out DiscordGuild guild) || guild.Roles is null) return;
        DiscordRole[] currentRoles = guild.Roles;
        int idx = Array.FindIndex(currentRoles, r => r.Id == roleId);

        if (idx < 0) return;
        DiscordRole[] newRoles = new DiscordRole[currentRoles.Length - 1];
        if (idx > 0)
            currentRoles.AsSpan(0, idx).CopyTo(newRoles.AsSpan());
        if (idx < currentRoles.Length - 1)
            currentRoles.AsSpan(idx + 1).CopyTo(newRoles.AsSpan(idx));

        guild.Roles = newRoles;
    }

    public void SetEmojis(ulong guildId, DiscordEmoji[] emojis)
    {
        if (_guilds.TryGetValue(guildId, out DiscordGuild guild))
        {
            guild.Emojis = emojis;
        }
    }

    public bool TryGetGuild(ulong guildId, out DiscordGuild guild) => _guilds.TryGetValue(guildId, out guild!);
    public bool TryGetUser(ulong userId, out DiscordUser user)
    {
        foreach (ObservableConcurrentList<DiscordMember> members in _membersByGuild.Values)
        {
            DiscordMember? member = members.FirstOrDefault(m => m.User.Id == userId);
            if (member == null) continue;
            user = member.User;
            return true;
        }
        user = null!;
        return false;
    }
}
