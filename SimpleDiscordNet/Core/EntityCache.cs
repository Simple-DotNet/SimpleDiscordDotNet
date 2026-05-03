using System.Collections.Concurrent;
using System.Globalization;
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
    private const int MaxUsers = 100000;

    private readonly ObservableConcurrentDictionary<ulong, DiscordGuild> _guilds;
    private readonly ConcurrentDictionary<ulong, ObservableConcurrentList<DiscordChannel>> _channelsByGuild = new();
    private readonly ConcurrentDictionary<ulong, ObservableConcurrentList<DiscordMember>> _membersByGuild = new();
    private readonly ConcurrentDictionary<ulong, DiscordUser> _users = new();
    private readonly ConcurrentQueue<ulong> _userAccessOrder = new();
    private readonly ConcurrentDictionary<ulong, DiscordChannel> _channelsById = new();
    private readonly ConcurrentDictionary<ulong, DiscordRole> _rolesById = new();
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
            _channelsByGuild.AddOrUpdate(guildId,
                _ =>
                {
                    var list = new ObservableConcurrentList<DiscordChannel>(_synchronizationContext);
                    list.AddRange(channels);
                    foreach (DiscordChannel channel in channels)
                    {
                        _channelsById[channel.Id] = channel;
                    }
                    return list;
                },
                (_, existing) =>
                {
                    existing.ReplaceAll(channels);
                    foreach (DiscordChannel channel in channels)
                    {
                        _channelsById[channel.Id] = channel;
                    }
                    return existing;
                });
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

        // Use AddOrUpdate to preserve existing list object for UI bindings
        _channelsByGuild.AddOrUpdate(guildId,
            _ =>
            {
                var list = new ObservableConcurrentList<DiscordChannel>(_synchronizationContext);
                list.AddRange(channelList);
                foreach (DiscordChannel channel in channelList)
                {
                    _channelsById[channel.Id] = channel;
                }
                return list;
            },
            (_, existing) =>
            {
                existing.ReplaceAll(channelList);
                foreach (DiscordChannel channel in channelList)
                {
                    _channelsById[channel.Id] = channel;
                }
                return existing;
            });
    }

    public void SetMembers(ulong guildId, IEnumerable<DiscordMember> members)
    {
        if (!_guilds.TryGetValue(guildId, out DiscordGuild guild))
        {
            // Guild not in cache yet - store members as-is (Guild will be set later via UpsertMember)
            var list = new ObservableConcurrentList<DiscordMember>(_synchronizationContext);
            list.AddRange(members);
            _membersByGuild[guildId] = list;
            foreach (DiscordMember member in members)
            {
                RecordUserAccess(member.User.Id);
            }
            return;
        }

        // Ensure all members have the Guild property set
        List<DiscordMember> memberList = [];
        foreach (DiscordMember member in members)
        {
            if (member.Guild?.Id == guildId)
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

        // Use AddOrUpdate to preserve existing list object for UI bindings
        _membersByGuild.AddOrUpdate(guildId,
            _ =>
            {
                var list = new ObservableConcurrentList<DiscordMember>(_synchronizationContext);
                list.AddRange(memberList);
                foreach (DiscordMember member in memberList)
                {
                    RecordUserAccess(member.User.Id);
                }
                return list;
            },
            (_, existing) =>
            {
                existing.ReplaceAll(memberList);
                return existing;
            });
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
        Dictionary<ulong, DiscordUser> distinctUsers = new();
        Dictionary<ulong, List<DiscordGuild>> userGuilds = new();

        foreach ((ulong gid, DiscordGuild guild) in _guilds)
        {
            if (!_membersByGuild.TryGetValue(gid, out ObservableConcurrentList<DiscordMember>? members)) continue;
            var memberSnapshot = members.ToArray();
            foreach (DiscordMember member in memberSnapshot)
            {
                if (!distinctUsers.ContainsKey(member.User.Id))
                {
                    distinctUsers[member.User.Id] = member.User;
                }
                if (!userGuilds.TryGetValue(member.User.Id, out List<DiscordGuild>? guilds))
                {
                    guilds = new List<DiscordGuild>();
                    userGuilds[member.User.Id] = guilds;
                }
                guilds.Add(guild);
            }
        }

        // Update each user's Guilds array
        foreach ((ulong userId, List<DiscordGuild> guilds) in userGuilds)
        {
            if (distinctUsers.TryGetValue(userId, out DiscordUser? user))
            {
                user.Guilds = guilds.ToArray();
            }
        }

        // Add users from the standalone user cache that aren't already present
        foreach (var kvp in _users)
        {
            if (!distinctUsers.ContainsKey(kvp.Key))
            {
                // Ensure Guilds array is set (empty array for users not in any guild)
                kvp.Value.Guilds ??= [];
                distinctUsers[kvp.Key] = kvp.Value;
            }
        }

        return distinctUsers.Values.ToArray();
    }

    public IReadOnlyList<DiscordRole> SnapshotRoles()
    {
        List<DiscordRole> list = new(1024);
        foreach ((ulong gid, DiscordGuild guild) in _guilds)
        {
            DiscordRole[]? roles;
            lock (guild)
                roles = guild.Roles;
            if (roles == null) continue;
            list.EnsureCapacity(list.Count + roles.Length);
            foreach (var role in roles)
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
            .Where(g => ShardCalculator.CalculateShardId(g.Id.ToString(CultureInfo.InvariantCulture).AsSpan(), totalShards) == shardId)
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
            if (ShardCalculator.CalculateShardId(gid.ToString(CultureInfo.InvariantCulture).AsSpan(), totalShards) != shardId)
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
            if (ShardCalculator.CalculateShardId(gid.ToString(CultureInfo.InvariantCulture).AsSpan(), totalShards) != shardId)
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
            if (ShardCalculator.CalculateShardId(gid.ToString(CultureInfo.InvariantCulture).AsSpan(), totalShards) != shardId)
                continue;

            DiscordRole[]? roles;
            lock (guild)
                roles = guild.Roles;
            if (roles == null) continue;
            list.EnsureCapacity(list.Count + roles.Length);
            foreach (DiscordRole role in roles)
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
        _guilds.TryRemove(guildId, out var guild);
        _channelsByGuild.TryRemove(guildId, out var oldChannels);
        _membersByGuild.TryRemove(guildId, out _);

        if (oldChannels != null)
        {
            foreach (var ch in oldChannels.ToArray())
            {
                _channelsById.TryRemove(ch.Id, out _);
            }
        }
        DiscordRole[]? roles;
        if (guild != null)
        {
            lock (guild)
                roles = guild.Roles;
            if (roles != null)
            {
                foreach (var role in roles)
                {
                    _rolesById.TryRemove(role.Id, out _);
                }
            }
        }
    }

    public void UpsertChannel(ulong guildId, DiscordChannel channel)
    {
        // Ensure channel has Guild property set
        if (channel.Guild?.Id != guildId && _guilds.TryGetValue(guildId, out DiscordGuild? guild))
        {
            channel.Guild = guild;
        }

        ObservableConcurrentList<DiscordChannel> list = _channelsByGuild.GetOrAdd(guildId, _ => new ObservableConcurrentList<DiscordChannel>(_synchronizationContext));

        // Atomic add-or-update under a single write lock
        list.AddOrUpdate(c => c.Id == channel.Id, channel);

        _channelsById[channel.Id] = channel;
    }

    public void RemoveChannel(ulong guildId, ulong channelId)
    {
        if (_channelsByGuild.TryGetValue(guildId, out ObservableConcurrentList<DiscordChannel>? list))
        {
            list.Remove(c => c.Id == channelId);
        }
        _channelsById.TryRemove(channelId, out _);
    }

    public void UpsertMember(ulong guildId, DiscordMember member)
    {
        // Ensure member has Guild property set
        DiscordMember memberWithGuild = member;
        if (member.Guild?.Id != guildId && _guilds.TryGetValue(guildId, out DiscordGuild guild))
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
        _users[memberWithGuild.User.Id] = memberWithGuild.User;
        RecordUserAccess(memberWithGuild.User.Id);

        ObservableConcurrentList<DiscordMember> list = _membersByGuild.GetOrAdd(guildId, _ => new ObservableConcurrentList<DiscordMember>(_synchronizationContext));

        // Atomic add-or-update under a single write lock
        list.AddOrUpdate(m => m.User.Id == memberWithGuild.User.Id, memberWithGuild);
    }

    public void RemoveMember(ulong guildId, ulong userId)
    {
        if (!_membersByGuild.TryGetValue(guildId, out ObservableConcurrentList<DiscordMember>? list)) return;
        list.Remove(m => m.User.Id == userId);
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

        lock (guild)
        {
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

        _rolesById[roleWithGuild.Id] = roleWithGuild;
    }

    public void UpsertUser(DiscordUser user)
    {
        // Preserve Bot field if it was already set and the new user doesn't have it
        if (_users.TryGetValue(user.Id, out DiscordUser? existing))
        {
            // If existing user has Bot field set, preserve it when new user doesn't
            if (existing.Bot.HasValue && !user.Bot.HasValue)
            {
                user.Bot = existing.Bot;
            }
        }
        _users[user.Id] = user;
        RecordUserAccess(user.Id);
    }

    public void RemoveRole(ulong guildId, ulong roleId)
    {
        if (!_guilds.TryGetValue(guildId, out DiscordGuild guild)) return;

        lock (guild)
        {
            if (guild.Roles is null) return;
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

        _rolesById.TryRemove(roleId, out _);
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
        if (_users.TryGetValue(userId, out user!))
        {
            RecordUserAccess(userId);
            return true;
        }

        foreach (ObservableConcurrentList<DiscordMember> members in _membersByGuild.Values)
        {
            DiscordMember? member = members.FirstOrDefault(m => m.User.Id == userId);
            if (member == null) continue;
            user = member.User;
            // Store in users cache for future lookups
            _users[userId] = user;
            RecordUserAccess(userId);
            return true;
        }
        user = null!;
        return false;
    }

    public bool TryGetMember(ulong guildId, ulong userId, out DiscordMember member)
    {
        if (_membersByGuild.TryGetValue(guildId, out var list))
        {
            foreach (var m in list.ToArray())
            {
                if (m.User.Id == userId)
                {
                    member = m;
                    return true;
                }
            }
        }
        member = null!;
        return false;
    }

    public bool TryGetChannel(ulong channelId, out DiscordChannel channel)
    {
        if (_channelsById.TryGetValue(channelId, out var ch))
        {
            channel = ch;
            return true;
        }
        channel = null!;
        return false;
    }

    public bool TryGetRole(ulong roleId, out DiscordRole role)
    {
        if (_rolesById.TryGetValue(roleId, out var r))
        {
            role = r;
            return true;
        }
        role = null!;
        return false;
    }

    // --- LRU eviction for user cache ---

    private void RecordUserAccess(ulong userId)
    {
        _userAccessOrder.Enqueue(userId);
        TrimUsersIfNeeded();
        DrainStaleEntries();
    }

    private void DrainStaleEntries()
    {
        int drained = 0;
        while (drained < 32 && _userAccessOrder.TryDequeue(out ulong userId))
        {
            if (_users.ContainsKey(userId))
            {
                _userAccessOrder.Enqueue(userId);
                break;
            }
            drained++;
        }
    }

    private void TrimUsersIfNeeded()
    {
        if (_users.Count <= MaxUsers)
            return;

        int excess = _users.Count - MaxUsers;
        int removed = 0;
        int maxAttempts = excess * 3;

        while (removed < excess && maxAttempts-- > 0 && _userAccessOrder.TryDequeue(out ulong userId))
        {
            if (_users.TryRemove(userId, out _))
            {
                removed++;
            }
        }
    }
}
