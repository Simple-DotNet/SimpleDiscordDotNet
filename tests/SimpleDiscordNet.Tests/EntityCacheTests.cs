using System.Collections.Specialized;
using System.Diagnostics;
using SimpleDiscordNet.Collections;
using SimpleDiscordNet.Core;
using SimpleDiscordNet.Entities;
using Xunit;

namespace SimpleDiscordNet.Tests;

public class EntityCacheTests
{
    private static DiscordMember Member(ulong id, string? nick = null)
        => new() { User = new DiscordUser { Id = id, Username = "u" }, Nick = nick };

    private static DiscordGuild Guild(ulong id = 1, string name = "g") => new() { Id = id, Name = name };

    [Fact]
    public void Guild_update_keeps_the_cached_instance_and_its_roles()
    {
        EntityCache cache = new();
        DiscordGuild original = Guild();
        original.Roles = [new DiscordRole { Id = 5, Name = "r", Guild = original }];
        original.Member_Count = 10;
        cache.UpsertGuild(original);

        DiscordGuild merged = cache.UpsertGuild(Guild(name: "renamed"));

        Assert.Same(original, merged);
        Assert.Equal("renamed", original.Name);
        Assert.Single(original.Roles!);
        Assert.Equal(10, original.Member_Count);
    }

    [Fact]
    public void Guild_update_replaces_roles_when_the_payload_has_them()
    {
        EntityCache cache = new();
        DiscordGuild original = Guild();
        original.Roles = [new DiscordRole { Id = 5, Name = "old", Guild = original }];
        cache.UpsertGuild(original);

        DiscordGuild update = Guild();
        update.Roles = [new DiscordRole { Id = 6, Name = "new", Guild = update }];
        cache.UpsertGuild(update);

        Assert.False(cache.TryGetRole(5, out _));
        Assert.True(cache.TryGetRole(6, out DiscordRole role));
        Assert.Same(original, role.Guild);
    }

    [Fact]
    public void Roles_are_indexed_from_guild_create()
    {
        EntityCache cache = new();
        DiscordGuild guild = Guild();
        guild.Roles = [new DiscordRole { Id = 5, Name = "r", Guild = guild }];

        cache.UpsertGuild(guild);

        Assert.True(cache.TryGetRole(5, out _));
    }

    [Fact]
    public void Dm_channels_are_found_by_id()
    {
        EntityCache cache = new();

        cache.UpsertDmChannel(new DiscordChannel { Id = 555, Type = (int)ChannelType.DM });

        Assert.True(cache.TryGetChannel(555, out DiscordChannel channel));
        Assert.Equal(ChannelType.DM, channel.ChannelType);
    }

    [Fact]
    public void Removing_a_guild_clears_its_members_from_the_index()
    {
        EntityCache cache = new();
        cache.UpsertGuild(Guild());
        cache.SetMembers(1, [Member(7)]);

        cache.RemoveGuild(1);

        Assert.False(cache.TryGetMember(1, 7, out _));
    }

    [Fact]
    public void Set_members_points_them_at_the_cached_guild()
    {
        EntityCache cache = new();
        DiscordGuild guild = cache.UpsertGuild(Guild());

        cache.SetMembers(1, [Member(7)]);

        cache.TryGetMember(1, 7, out DiscordMember member);
        Assert.Same(guild, member.Guild);
    }

    [Fact]
    public void Set_channels_accepts_the_guilds_own_live_list()
    {
        EntityCache cache = new();
        cache.SetChannels(1, [new DiscordChannel { Id = 2, Type = 0 }]);

        cache.SetChannels(1, cache.GetLiveChannels(1)!);

        Assert.Single(cache.GetLiveChannels(1)!.ToArray());
    }

    [Fact]
    public void Member_batch_adds_new_members_and_replaces_existing_ones()
    {
        EntityCache cache = new();
        cache.UpsertGuild(Guild());
        cache.UpsertMember(1, Member(1, "old"));

        cache.UpsertMembers(1, [Member(1, "new"), Member(2)]);

        DiscordMember[] members = cache.GetLiveMembers(1)!.ToArray();
        Assert.Equal(2, members.Length);
        Assert.Equal("new", members.Single(m => m.User.Id == 1).Nick);
    }

    [Fact]
    public void Removed_members_can_be_added_again()
    {
        EntityCache cache = new();
        cache.UpsertMember(1, Member(1));

        cache.RemoveMember(1, 1);
        cache.UpsertMember(1, Member(1));

        Assert.Single(cache.GetLiveMembers(1)!.ToArray());
    }

    [Fact]
    public void Loading_a_large_guild_in_chunks_stays_fast()
    {
        EntityCache cache = new();
        cache.UpsertGuild(Guild());
        Stopwatch stopwatch = Stopwatch.StartNew();

        for (ulong chunk = 0; chunk < 100; chunk++)
        {
            cache.UpsertMembers(1, Enumerable.Range(0, 1000).Select(i => Member((chunk * 1000) + (ulong)i + 1)).ToArray());
        }

        Assert.Equal(100_000, cache.GetLiveMembers(1)!.ToArray().Length);
        Assert.True(stopwatch.ElapsedMilliseconds < 5000, $"Took {stopwatch.ElapsedMilliseconds}ms.");
    }

    [Fact]
    public void User_cache_is_capped_and_evicts_oldest_first()
    {
        EntityCache cache = new();

        for (ulong id = 1; id <= 100_010; id++)
        {
            cache.UpsertUser(new DiscordUser { Id = id, Username = "u" });
        }

        Assert.False(cache.TryGetUser(1, out _));
        Assert.True(cache.TryGetUser(100_010, out _));
    }
}

public class ObservableCollectionTests
{
    [Fact]
    public void List_bulk_operations_raise_a_reset()
    {
        ObservableConcurrentList<int> list = new();
        List<NotifyCollectionChangedAction> actions = [];
        list.CollectionChanged += (_, e) => actions.Add(e.Action);

        list.AddRange([1, 2]);
        list.ReplaceAll([3]);
        list.RemoveRange([3]);
        list.Clear();

        Assert.Equal(4, actions.Count);
        Assert.All(actions, action => Assert.Equal(NotifyCollectionChangedAction.Reset, action));
    }

    [Fact]
    public void Dictionary_bulk_replace_raises_a_reset()
    {
        ObservableConcurrentDictionary<int, string> dictionary = new();
        List<NotifyCollectionChangedAction> actions = [];
        dictionary.CollectionChanged += (_, e) => actions.Add(e.Action);

        dictionary.ReplaceAll([new KeyValuePair<int, string>(1, "a")]);

        Assert.Equal(NotifyCollectionChangedAction.Reset, Assert.Single(actions));
    }
}
