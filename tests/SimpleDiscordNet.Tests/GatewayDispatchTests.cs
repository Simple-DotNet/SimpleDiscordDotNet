using SimpleDiscordNet.Commands;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Events;
using SimpleDiscordNet.Gateway;
using SimpleDiscordNet.Models;
using SimpleDiscordNet.Primitives;
using Xunit;

namespace SimpleDiscordNet.Tests;

public class GatewayDispatchTests : IDisposable
{
    private readonly GatewayClient _gateway = new("token", DiscordIntents.Guilds, Json.Options, shardId: 2, totalShards: 4);
    private readonly List<Exception> _errors = [];

    public GatewayDispatchTests()
    {
        _gateway.Error += (_, ex) => _errors.Add(ex);
    }

    public void Dispose() => _gateway.Dispose();

    private InteractionCreateEvent DispatchInteraction(string json)
    {
        InteractionCreateEvent? received = null;
        _gateway.InteractionCreate += (_, e) => received = e;

        _gateway.HandleDispatch("INTERACTION_CREATE", Json.Parse(json));

        Assert.Empty(_errors);
        return Assert.IsType<InteractionCreateEvent>(received);
    }

    private static string Interaction(int type, string data, string extra = "")
        => $$"""{"id":"1","token":"t","application_id":"2","type":{{type}},"guild_id":"3","channel_id":"4","member":{{Json.Member}},"data":{{data}}{{extra}}}""";

    [Fact]
    public void Guild_delete_during_an_outage_is_ignored()
    {
        bool deleted = false;
        _gateway.GuildDelete += (_, _) => deleted = true;

        _gateway.HandleDispatch("GUILD_DELETE", Json.Parse("""{"id":"1","unavailable":true}"""));

        Assert.False(deleted);
    }

    [Fact]
    public void Guild_delete_when_leaving_is_raised()
    {
        ulong? deleted = null;
        _gateway.GuildDelete += (_, id) => deleted = id;

        _gateway.HandleDispatch("GUILD_DELETE", Json.Parse("""{"id":"1"}"""));

        Assert.Equal(1UL, deleted);
    }

    [Fact]
    public void Guild_update_carries_the_full_guild()
    {
        DiscordGuild? guild = null;
        _gateway.GuildUpdate += (_, g) => guild = g;

        _gateway.HandleDispatch("GUILD_UPDATE", Json.Parse(Json.Guild));

        Assert.Equal(73193882359173120UL, guild!.Owner_Id);
        Assert.Same(guild, Assert.Single(guild.Roles!).Guild);
    }

    [Fact]
    public void Guild_create_includes_channels_members_and_threads()
    {
        GuildCreateEvent? created = null;
        _gateway.GuildCreate += (_, e) => created = e;
        string json = Json.Guild.TrimEnd('}') + ",\"channels\":[" + Json.Channel + "],\"members\":[" + Json.Member + "],\"threads\":[]}";

        _gateway.HandleDispatch("GUILD_CREATE", Json.Parse(json));

        Assert.Empty(_errors);
        Assert.Equal(197038439483310086UL, Assert.Single(created!.Channels!).Guild_Id);
        Assert.Equal("2015-04-26T06:26:56.936000+00:00", Assert.Single(created.Members!).Joined_At);
    }

    [Fact]
    public void Member_events_keep_all_member_fields()
    {
        GatewayMemberEvent? added = null;
        _gateway.GuildMemberAdd += (_, e) => added = e;
        string json = Json.Member.TrimEnd('}') + ",\"guild_id\":\"3\"}";

        _gateway.HandleDispatch("GUILD_MEMBER_ADD", Json.Parse(json));

        Assert.Equal("2015-04-26T06:26:56.936000+00:00", added!.Member.Joined_At);
        Assert.Equal("Nell", added.Member.User.Global_Name);
    }

    [Fact]
    public void A_presence_user_with_only_an_id_parses()
    {
        PresenceUpdateEvent? presence = null;
        _gateway.PresenceUpdate += (_, e) => presence = e;

        _gateway.HandleDispatch("PRESENCE_UPDATE", Json.Parse("""{"guild_id":"1","user":{"id":"2"},"status":"online","activities":[]}"""));

        Assert.Empty(_errors);
        Assert.Equal(2UL, presence!.User.Id);
    }

    [Fact]
    public void Message_create_carries_the_full_author()
    {
        MessageCreateEventRaw? message = null;
        _gateway.MessageCreate += (_, e) => message = e;

        _gateway.HandleDispatch("MESSAGE_CREATE", Json.Parse(Json.Message));

        Assert.Equal("Nell", message!.AuthorUser!.Global_Name);
    }

    [Fact]
    public void A_bad_payload_reports_an_error_instead_of_throwing()
    {
        _gateway.GuildDelete += (_, _) => { };

        _gateway.HandleDispatch("GUILD_DELETE", Json.Parse("""{"unexpected":true}"""));

        Assert.Single(_errors);
    }

    [Fact]
    public void Slash_command_subcommand_path_and_options_are_read()
    {
        InteractionCreateEvent e = DispatchInteraction(Interaction(2, """
            {"name":"admin","options":[{"name":"users","type":2,"options":[{"name":"ban","type":1,"options":[
              {"name":"who","type":3,"value":"x"},{"name":"days","type":4,"value":3},{"name":"rate","type":10,"value":2.5},{"name":"yes","type":5,"value":true}
            ]}]}]}
            """));

        Assert.Equal("users", e.Data!.SubcommandGroup);
        Assert.Equal("ban", e.Data.Subcommand);
        Assert.Equal("x", e.Data.Options.Single(o => o.Name == "who").String);
        Assert.Equal(3, e.Data.Options.Single(o => o.Name == "days").Integer);
        Assert.Equal(2.5, e.Data.Options.Single(o => o.Name == "rate").Number);
        Assert.True(e.Data.Options.Single(o => o.Name == "yes").Boolean);
    }

    [Fact]
    public void Shard_id_is_set_on_interactions()
    {
        Assert.Equal(2, DispatchInteraction(Interaction(2, """{"name":"ping"}""")).ShardId);
    }

    [Fact]
    public void User_context_menu_exposes_its_target_not_the_invoker()
    {
        InteractionCreateEvent e = DispatchInteraction(Interaction(2, """
            {"name":"Info","type":2,"target_id":"99","resolved":{
              "users":{"99":{"id":"99","username":"target"}},
              "members":{"99":{"nick":"TargetNick","roles":[],"joined_at":"2020-01-01T00:00:00+00:00"}}
            }}
            """));
        InteractionContext ctx = new(null!, e);

        Assert.Equal("99", ctx.TargetId);
        Assert.Equal("target", ctx.TargetUser!.Username);
        Assert.Equal("TargetNick", ctx.TargetMember!.Nick);
        Assert.Equal("Nick", ctx.Member!.Nick);
    }

    [Fact]
    public void Message_context_menu_exposes_its_target_message()
    {
        InteractionCreateEvent e = DispatchInteraction(Interaction(2, """{"name":"Report","type":3,"target_id":"334385199974967042","resolved":{"messages":{"334385199974967042":""" + Json.Message + "}}}"));

        Assert.Equal("hi", new InteractionContext(null!, e).TargetMessage!.Content);
    }

    [Fact]
    public void Select_menu_resolved_data_reaches_the_context()
    {
        InteractionCreateEvent e = DispatchInteraction(Interaction(3, """{"custom_id":"pick","component_type":6,"values":["41771983423143936"],"resolved":{"roles":{"41771983423143936":""" + Json.Role + "}}}"));

        DiscordRole role = Assert.Single(new InteractionContext(null!, e).GetResolvedRoles());
        Assert.Equal(3447003, role.Color);
    }

    [Fact]
    public void Autocomplete_handles_a_numeric_sibling_option()
    {
        InteractionCreateEvent e = DispatchInteraction(Interaction(4, """
            {"name":"search","options":[{"name":"count","type":4,"value":5},{"name":"term","type":3,"value":"ab","focused":true}]}
            """));

        Assert.Equal(5, e.Data!.Options[0].Integer);
        Assert.True(e.Data.Options[1].Focused);
    }

    [Fact]
    public void Autocomplete_reads_a_focused_number_as_text()
    {
        InteractionCreateEvent e = DispatchInteraction(Interaction(4, """{"name":"search","options":[{"name":"count","type":4,"value":12,"focused":true}]}"""));

        Assert.Equal("12", e.Data!.Options[0].String);
    }

    [Fact]
    public void Autocomplete_in_a_subcommand_keeps_the_path()
    {
        InteractionCreateEvent e = DispatchInteraction(Interaction(4, """
            {"name":"tools","options":[{"name":"find","type":1,"options":[{"name":"term","type":3,"value":"a","focused":true}]}]}
            """));

        Assert.Equal("find", e.Data!.Subcommand);
        Assert.Equal("term", Assert.Single(e.Data.Options).Name);
    }

    [Fact]
    public void Modal_inputs_are_read()
    {
        InteractionCreateEvent e = DispatchInteraction(Interaction(5, """
            {"custom_id":"form","components":[{"type":1,"components":[{"type":4,"custom_id":"name","value":"Bob"}]}]}
            """));

        Assert.Equal("Bob", new InteractionContext(null!, e).GetModalValue("name"));
    }
}

[Collection(nameof(StaticEventsCollection))]
public class MessagePathTests : IDisposable
{
    private readonly DiscordBot _bot = DiscordBot.NewBuilder().WithToken("x").Build();

    public void Dispose()
    {
        DiscordEvents.UnsubscribeAll();
        _bot.Dispose();
    }

    private static MessageCreateEventRaw Raw(string? guildId = null) => new()
    {
        Id = "1",
        ChannelId = "2",
        GuildId = guildId,
        Content = "hi",
        Author = new Author { Id = 3, Username = "u" }
    };

    [Fact]
    public void Direct_messages_reach_both_events_without_a_cached_channel()
    {
        MessageCreateEvent? created = null;
        DirectMessageEvent? direct = null;
        DiscordEvents.MessageCreated += (_, e) => created = e;
        DiscordEvents.DirectMessageReceived += (_, e) => direct = e;

        _bot.OnMessageCreate(null, Raw());

        Assert.Equal(ChannelType.DM, created!.Channel.ChannelType);
        Assert.NotNull(direct);
    }

    [Fact]
    public void Guild_messages_in_uncached_channels_still_arrive()
    {
        MessageCreateEvent? created = null;
        DiscordEvents.MessageCreated += (_, e) => created = e;

        _bot.OnMessageCreate(null, Raw("9"));

        Assert.Equal(9UL, created!.Channel.Guild_Id);
    }
}

[CollectionDefinition(nameof(StaticEventsCollection), DisableParallelization = true)]
public class StaticEventsCollection;
