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
    private readonly ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, DiscordMember>> _memberIndexByGuild = new();
    private readonly ConcurrentDictionary<ulong, DiscordUser> _users = new();
    private readonly ConcurrentQueue<ulong> _userInsertOrder = new();
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

    public void SetChannels(ulong guildId, IEnumerable<DiscordChannel> channels)
    {
        // Materialize first: the caller may pass this guild's live list, which is about to be replaced.
        DiscordChannel[] channelArray = channels.ToArray();

        if (_guilds.TryGetValue(guildId, out DiscordGuild? guild))
        {
            foreach (DiscordChannel channel in channelArray)
            {
                channel.Guild = guild;
            }
        }

        // Use GetOrAdd to preserve the existing list object for UI bindings
        ObservableConcurrentList<DiscordChannel> list = _channelsByGuild.GetOrAdd(guildId, _ => new ObservableConcurrentList<DiscordChannel>(_synchronizationContext));

        foreach (DiscordChannel previous in list.ToArray())
        {
            _channelsById.TryRemove(previous.Id, out _);
        }

        list.ReplaceAll(channelArray);

        foreach (DiscordChannel channel in channelArray)
        {
            _channelsById[channel.Id] = channel;
        }
    }

    /// <summary>
    /// Caches a DM channel. DM channels have no guild, so they are only reachable by channel id.
    /// </summary>
    public void UpsertDmChannel(DiscordChannel channel)
    {
        _channelsById[channel.Id] = channel;
    }

    public void SetMembers(ulong guildId, IEnumerable<DiscordMember> members)
    {
        DiscordMember[] memberArray = members.ToArray();

        if (_guilds.TryGetValue(guildId, out DiscordGuild? guild))
        {
            foreach (DiscordMember member in memberArray)
            {
                member.Guild = guild;
            }
        }

        ConcurrentDictionary<ulong, DiscordMember> index = GetMemberIndex(guildId);
        index.Clear();
        foreach (DiscordMember member in memberArray)
        {
            index[member.User.Id] = member;
        }

        // Use GetOrAdd to preserve the existing list object for UI bindings
        ObservableConcurrentList<DiscordMember> list = _membersByGuild.GetOrAdd(guildId, _ => new ObservableConcurrentList<DiscordMember>(_synchronizationContext));
        list.ReplaceAll(memberArray);
    }

    private ConcurrentDictionary<ulong, DiscordMember> GetMemberIndex(ulong guildId)
        => _memberIndexByGuild.GetOrAdd(guildId, static _ => new ConcurrentDictionary<ulong, DiscordMember>());

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

    public void ResetAll()
    {
        _guilds.Clear();
        _channelsByGuild.Clear();
        _membersByGuild.Clear();
        _memberIndexByGuild.Clear();
        _users.Clear();
        _channelsById.Clear();
        _rolesById.Clear();
        while (_userInsertOrder.TryDequeue(out _)) { }
    }

    /// <summary>
    /// Adds a guild or merges it into the cached instance, keeping references held by channels,
    /// members and roles valid. Returns the instance that is now in the cache.
    /// </summary>
    public DiscordGuild UpsertGuild(DiscordGuild guild)
    {
        DiscordGuild cached = guild;
        if (_guilds.TryGetValue(guild.Id, out DiscordGuild? existing) && !ReferenceEquals(existing, guild))
        {
            lock (existing)
            {
                if (guild.Roles is not null && existing.Roles is not null)
                {
                    foreach (DiscordRole role in existing.Roles)
                    {
                        _rolesById.TryRemove(role.Id, out _);
                    }
                }
                existing.CopyFrom(guild);
            }
            cached = existing;
        }

        DiscordRole[]? roles;
        lock (cached)
            roles = cached.Roles;
        if (roles is not null)
        {
            foreach (DiscordRole role in roles)
            {
                role.Guild = cached;
                _rolesById[role.Id] = role;
            }
        }

        _guilds[cached.Id] = cached;
        return cached;
    }

    public void RemoveGuild(ulong guildId)
    {
        _guilds.TryRemove(guildId, out var guild);
        _channelsByGuild.TryRemove(guildId, out var oldChannels);
        _membersByGuild.TryRemove(guildId, out _);
        _memberIndexByGuild.TryRemove(guildId, out _);

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
        if (_guilds.TryGetValue(guildId, out DiscordGuild? guild))
        {
            member.Guild = guild;
        }

        StoreUser(member.User);

        ConcurrentDictionary<ulong, DiscordMember> index = GetMemberIndex(guildId);
        bool exists = index.ContainsKey(member.User.Id);
        index[member.User.Id] = member;

        ObservableConcurrentList<DiscordMember> list = _membersByGuild.GetOrAdd(guildId, _ => new ObservableConcurrentList<DiscordMember>(_synchronizationContext));

        // A member the index hasn't seen can't be in the list, so only updates need a scan
        if (exists)
        {
            list.AddOrUpdate(m => m.User.Id == member.User.Id, member);
        }
        else
        {
            list.Add(member);
        }
    }

    /// <summary>
    /// Adds or updates a batch of members, such as a GUILD_MEMBERS_CHUNK. New members are appended
    /// directly, so loading a large guild stays linear instead of scanning the list per member.
    /// </summary>
    public void UpsertMembers(ulong guildId, IReadOnlyList<DiscordMember> members)
    {
        if (members.Count == 0) return;

        _guilds.TryGetValue(guildId, out DiscordGuild? guild);
        ConcurrentDictionary<ulong, DiscordMember> index = GetMemberIndex(guildId);
        ObservableConcurrentList<DiscordMember> list = _membersByGuild.GetOrAdd(guildId, _ => new ObservableConcurrentList<DiscordMember>(_synchronizationContext));

        List<DiscordMember> added = new(members.Count);
        Dictionary<ulong, DiscordMember>? updated = null;

        foreach (DiscordMember member in members)
        {
            if (guild is not null)
            {
                member.Guild = guild;
            }

            StoreUser(member.User);

            if (index.ContainsKey(member.User.Id))
            {
                (updated ??= new Dictionary<ulong, DiscordMember>())[member.User.Id] = member;
            }
            else
            {
                added.Add(member);
            }
            index[member.User.Id] = member;
        }

        if (added.Count > 0)
        {
            list.AddRange(added);
        }

        if (updated is not null)
        {
            list.ReplaceEach(m => updated.TryGetValue(m.User.Id, out DiscordMember? replacement) ? replacement : m);
        }
    }

    public void RemoveMember(ulong guildId, ulong userId)
    {
        if (_memberIndexByGuild.TryGetValue(guildId, out ConcurrentDictionary<ulong, DiscordMember>? index))
        {
            index.TryRemove(userId, out _);
        }

        if (!_membersByGuild.TryGetValue(guildId, out ObservableConcurrentList<DiscordMember>? list)) return;
        list.Remove(m => m.User.Id == userId);
    }

    public void UpsertRole(ulong guildId, DiscordRole role)
    {
        if (!_guilds.TryGetValue(guildId, out DiscordGuild? guild)) return;

        DiscordRole roleWithGuild = role;
        roleWithGuild.Guild = guild;

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
        StoreUser(user);
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
            return true;
        }

        foreach (ConcurrentDictionary<ulong, DiscordMember> index in _memberIndexByGuild.Values)
        {
            if (!index.TryGetValue(userId, out DiscordMember? member)) continue;
            user = member.User;
            // Store in users cache for future lookups
            StoreUser(user);
            return true;
        }
        user = null!;
        return false;
    }

    public bool TryGetMember(ulong guildId, ulong userId, out DiscordMember member)
    {
        if (_memberIndexByGuild.TryGetValue(guildId, out ConcurrentDictionary<ulong, DiscordMember>? index) &&
            index.TryGetValue(userId, out DiscordMember? found))
        {
            member = found;
            return true;
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

    // --- First-in, first-out eviction for the user cache ---

    /// <summary>
    /// Stores a user. Only new users join the eviction queue, which keeps the queue the same
    /// size as the cache no matter how often existing users are refreshed.
    /// </summary>
    private void StoreUser(DiscordUser user)
    {
        bool isNew = !_users.ContainsKey(user.Id);
        _users[user.Id] = user;

        if (isNew)
        {
            _userInsertOrder.Enqueue(user.Id);
            TrimUsersIfNeeded();
        }
    }

    private void TrimUsersIfNeeded()
    {
        while (_users.Count > MaxUsers && _userInsertOrder.TryDequeue(out ulong userId))
        {
            _users.TryRemove(userId, out _);
        }
    }
}
