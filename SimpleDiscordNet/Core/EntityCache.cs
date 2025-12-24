using System.Collections.Concurrent;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Sharding;

namespace SimpleDiscordNet.Core;

/// <summary>
/// Thread-safe in-memory cache of Discord entities known to the bot.
/// </summary>
internal sealed class EntityCache
{
    private readonly ConcurrentDictionary<ulong, DiscordGuild> _guilds = new();
    private readonly ConcurrentDictionary<ulong, List<DiscordChannel>> _channelsByGuild = new();
    private readonly ConcurrentDictionary<ulong, List<DiscordMember>> _membersByGuild = new();

    public void ReplaceGuilds(IEnumerable<DiscordGuild> guilds)
    {
        _guilds.Clear();
        foreach (DiscordGuild g in guilds)
        {
            _guilds[g.Id] = g;
        }
    }

    public void SetChannels(ulong guildId, IEnumerable<DiscordChannel> channels)
    {
        if (!_guilds.TryGetValue(guildId, out DiscordGuild? guild))
        {
            // Guild not in cache yet - store channels as-is (Guild will be set later via UpsertChannel)
            _channelsByGuild[guildId] = channels.ToList();
            return;
        }

        // Ensure all channels have the Guild property set
        List<DiscordChannel> channelList = new();
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
        _channelsByGuild[guildId] = channelList;
    }

    public void SetMembers(ulong guildId, IEnumerable<DiscordMember> members)
    {
        if (!_guilds.TryGetValue(guildId, out DiscordGuild? guild))
        {
            // Guild not in cache yet - store members as-is (Guild will be set later via UpsertMember)
            _membersByGuild[guildId] = members.ToList();
            return;
        }

        // Ensure all members have the Guild property set
        List<DiscordMember> memberList = new();
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
        _membersByGuild[guildId] = memberList;
    }

    public IReadOnlyList<DiscordGuild> SnapshotGuilds() => _guilds.Values.OrderBy(g => g.Id).ToArray();

    public IReadOnlyList<DiscordChannel> SnapshotChannels()
    {
        List<DiscordChannel> list = new(1024);
        foreach ((ulong gid, DiscordGuild guild) in _guilds)
        {
            if (!_channelsByGuild.TryGetValue(gid, out List<DiscordChannel>? chs)) continue;
            list.EnsureCapacity(list.Count + chs.Count);
            for (int index = chs.Count - 1; index >= 0; index--)
            {
                list.Add(chs[index]);
            }
        }
        return list;
    }

    public IReadOnlyList<DiscordMember> SnapshotMembers()
    {
        List<DiscordMember> list = new(2048);
        foreach ((ulong gid, DiscordGuild guild) in _guilds)
        {
            if (!_membersByGuild.TryGetValue(gid, out List<DiscordMember>? members)) continue;
            list.EnsureCapacity(list.Count + members.Count);
            for (int index = members.Count - 1; index >= 0; index--)
            {
                list.Add(members[index]);
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
            if (!_membersByGuild.TryGetValue(gid, out List<DiscordMember>? members)) continue;
            foreach (DiscordMember member in members)
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
            foreach (List<DiscordMember> members in _membersByGuild.Values)
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

            if (!_channelsByGuild.TryGetValue(gid, out List<DiscordChannel>? chs)) continue;
            list.EnsureCapacity(list.Count + chs.Count);
            foreach (DiscordChannel c in chs)
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

            if (!_membersByGuild.TryGetValue(gid, out List<DiscordMember>? members)) continue;
            list.EnsureCapacity(list.Count + members.Count);
            foreach (DiscordMember member in members)
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

        List<DiscordChannel> list = _channelsByGuild.GetOrAdd(guildId, static _ => new List<DiscordChannel>());
        lock (list)
        {
            int idx = list.FindIndex(c => c.Id == channel.Id);
            if (idx >= 0) list[idx] = channel; else list.Add(channel);
        }
    }

    public void RemoveChannel(ulong guildId, ulong channelId)
    {
        if (_channelsByGuild.TryGetValue(guildId, out List<DiscordChannel>? list))
        {
            lock (list)
            {
                int idx = list.FindIndex(c => c.Id == channelId);
                if (idx >= 0) list.RemoveAt(idx);
            }
        }
    }

    public void UpsertMember(ulong guildId, DiscordMember member)
    {
        // Ensure member has Guild property set
        DiscordMember memberWithGuild = member;
        if (member.Guild.Id != guildId && _guilds.TryGetValue(guildId, out DiscordGuild? guild))
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

        List<DiscordMember> list = _membersByGuild.GetOrAdd(guildId, static _ => []);
        lock (list)
        {
            int idx = list.FindIndex(m => m.User.Id == memberWithGuild.User.Id);
            if (idx >= 0) list[idx] = memberWithGuild; else list.Add(memberWithGuild);
        }
    }

    public void RemoveMember(ulong guildId, ulong userId)
    {
        if (!_membersByGuild.TryGetValue(guildId, out List<DiscordMember>? list)) return;
        lock (list)
        {
            int idx = list.FindIndex(m => m.User.Id == userId);
            if (idx >= 0) list.RemoveAt(idx);
        }
    }

    public void UpsertRole(ulong guildId, DiscordRole role)
    {
        if (!_guilds.TryGetValue(guildId, out DiscordGuild? guild)) return;

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
        if (!_guilds.TryGetValue(guildId, out DiscordGuild? guild) || guild.Roles is null) return;
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
        if (_guilds.TryGetValue(guildId, out DiscordGuild? guild))
        {
            guild.Emojis = emojis;
        }
    }

    public bool TryGetGuild(ulong guildId, out DiscordGuild guild) => _guilds.TryGetValue(guildId, out guild!);
    public bool TryGetUser(ulong userId, out DiscordUser user)
    {
        foreach (List<DiscordMember> members in _membersByGuild.Values)
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
