using System.Net;
using System.Reflection;
using System.Text.Json;
using SimpleDiscordNet.Commands;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Events;
using SimpleDiscordNet.Gateway;
using SimpleDiscordNet.Models;
using SimpleDiscordNet.Models.Requests;
using SimpleDiscordNet.Primitives;
using SimpleDiscordNet.Rest;
using Xunit;

namespace SimpleDiscordNet.Tests;

/// <summary>A real bot wired to a stub HTTP handler, so tests see the exact route and body of a call.</summary>
internal sealed class BotFixture : IDisposable
{
    private readonly RestFixture _rest = new();

    public DiscordBot Bot { get; } = DiscordBot.NewBuilder().WithToken("x").Build();
    public StubHandler Handler => _rest.Handler;

    public BotFixture()
    {
        typeof(DiscordBot).GetField("_rest", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Bot, _rest.Rest);
    }

    public string LastRoute => Handler.Requests[^1].RequestUri!.PathAndQuery.Replace("/api/v10", string.Empty);

    public void RespondWith(string json)
        => Handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };

    public void Dispose()
    {
        Bot.Dispose();
        _rest.Dispose();
    }
}

public class PermissionFlagTests
{
    [Theory]
    [InlineData(PermissionFlags.CreateGuildExpressions, 43)]
    [InlineData(PermissionFlags.CreateEvents, 44)]
    [InlineData(PermissionFlags.SetVoiceChannelStatus, 48)]
    [InlineData(PermissionFlags.SendPolls, 49)]
    [InlineData(PermissionFlags.UseExternalApps, 50)]
    [InlineData(PermissionFlags.PinMessages, 51)]
    [InlineData(PermissionFlags.BypassSlowmode, 52)]
    public void New_permissions_use_Discords_bits(PermissionFlags flag, int bit)
    {
        Assert.Equal(1UL << bit, (ulong)flag);
    }
}

public class MessageBuilderFeatureTests
{
    private static JsonElement Serialize(MessageBuilder builder)
        => JsonSerializer.SerializeToElement(builder.Build(), Json.Options);

    [Fact]
    public void A_quick_poll_serializes_in_Discords_format()
    {
        JsonElement payload = Serialize(new MessageBuilder().WithPoll("Pizza or tacos?", "Pizza", "Tacos"));

        JsonElement poll = payload.GetProperty("poll");
        Assert.Equal("Pizza or tacos?", poll.GetProperty("question").GetProperty("text").GetString());
        Assert.Equal("Tacos", poll.GetProperty("answers")[1].GetProperty("poll_media").GetProperty("text").GetString());
    }

    [Fact]
    public void A_custom_poll_keeps_its_settings()
    {
        PollBuilder builder = new PollBuilder("Lunch?").AddAnswer("Pizza", "🍕").WithDuration(48).AllowMultipleAnswers();

        JsonElement poll = Serialize(new MessageBuilder().WithPoll(builder)).GetProperty("poll");

        Assert.Equal(48, poll.GetProperty("duration").GetInt32());
        Assert.True(poll.GetProperty("allow_multiselect").GetBoolean());
        Assert.Equal("🍕", poll.GetProperty("answers")[0].GetProperty("poll_media").GetProperty("emoji").GetProperty("name").GetString());
    }

    [Fact]
    public void A_poll_needs_an_answer()
    {
        Assert.Throws<InvalidOperationException>(() => new MessageBuilder().WithPoll("Empty?").Build());
    }

    [Fact]
    public void A_poll_can_have_at_most_ten_answers()
    {
        PollBuilder poll = new("Many?");
        for (int i = 0; i < 10; i++) poll.AddAnswer($"{i}");

        Assert.Throws<InvalidOperationException>(() => poll.AddAnswer("too many"));
    }

    [Fact]
    public void Plain_buttons_stay_in_an_action_row_without_the_layout_flag()
    {
        MessagePayload payload = new MessageBuilder().WithContent("hi").WithButton("Yes", "yes").Build();

        Assert.Null(payload.flags);
        Assert.Equal("hi", payload.content);
        Assert.IsType<ActionRow>(Assert.Single(payload.components!));
    }

    [Fact]
    public void Layout_components_set_the_flag_and_absorb_message_content()
    {
        MessagePayload payload = new MessageBuilder()
            .WithContent("Status")
            .WithContainer(new TextDisplay("Build passed"), new Button("Logs", "logs"))
            .Build();

        Assert.Equal(1 << 15, payload.flags);
        Assert.Null(payload.content);
        Assert.Equal("Status", Assert.IsType<TextDisplay>(payload.components![0]).content);

        Container container = Assert.IsType<Container>(payload.components[1]);
        Assert.IsType<ActionRow>(container.components[1]);
    }

    [Fact]
    public void Layout_components_serialize_with_their_type_numbers()
    {
        JsonElement payload = Serialize(new MessageBuilder()
            .WithText("# Title")
            .WithSeparator()
            .WithSection("Deploy?", new Button("Go", "go"))
            .WithGallery("https://example.com/a.png")
            .WithContainer(new FileComponent("report.pdf"))
            .WithComponents(new Container(new TextDisplay("x")).WithColor(DiscordColor.FromRgb(0, 255, 0))));

        JsonElement components = payload.GetProperty("components");
        Assert.Equal([10, 14, 9, 12, 17, 17], components.EnumerateArray().Select(c => c.GetProperty("type").GetInt32()));
        Assert.Equal(2, components[2].GetProperty("accessory").GetProperty("type").GetInt32());
        Assert.Equal("https://example.com/a.png", components[3].GetProperty("items")[0].GetProperty("media").GetProperty("url").GetString());
        Assert.Equal("attachment://report.pdf", components[4].GetProperty("components")[0].GetProperty("file").GetProperty("url").GetString());
        Assert.Equal(0x00FF00, components[5].GetProperty("accent_color").GetInt32());
    }

    [Fact]
    public void Layout_components_cant_be_mixed_with_embeds_or_polls()
    {
        Assert.Throws<InvalidOperationException>(() => new MessageBuilder().WithText("x").WithEmbed(new EmbedBuilder().WithTitle("t")).Build());
        Assert.Throws<InvalidOperationException>(() => new MessageBuilder().WithText("x").WithPoll("q", "a").Build());
    }

    [Fact]
    public void Clear_removes_polls_and_forum_settings()
    {
        MessageBuilder builder = new MessageBuilder().WithPoll("q", "a").WithForumPost("title", "1");

        builder.Clear();
        MessagePayload payload = builder.Build();

        Assert.Null(payload.poll);
        Assert.Null(payload.thread_name);
    }
}

public class ChannelFeatureTests : IDisposable
{
    private readonly GatewayClient _gateway = new("token", DiscordIntents.Guilds, Json.Options);
    private readonly List<Exception> _errors = [];

    public ChannelFeatureTests() => _gateway.Error += (_, ex) => _errors.Add(ex);

    public void Dispose() => _gateway.Dispose();

    private DiscordChannel Dispatch(string json)
    {
        DiscordChannel? received = null;
        _gateway.ChannelCreate += (_, channel) => received = channel;
        _gateway.HandleDispatch("CHANNEL_CREATE", Json.Parse(json));
        Assert.Empty(_errors);
        return Assert.IsType<DiscordChannel>(received);
    }

    [Fact]
    public void Hidden_channels_are_detected_from_the_flag_not_the_name()
    {
        // Discord nulls or reduces other fields, so this keeps only what it promises to send
        DiscordChannel hidden = Dispatch("""{"id":"1","guild_id":"2","type":0,"position":3,"parent_id":null,"name":"___hidden___","topic":null,"flags":131072,"permission_overwrites":[{"id":"2","type":0,"allow":"0","deny":"1024"}]}""");

        Assert.True(hidden.IsObfuscated);
    }

    [Fact]
    public void Ordinary_channels_arent_hidden()
    {
        Assert.False(Dispatch(Json.Channel).IsObfuscated);
    }

    [Fact]
    public void Forum_tags_and_thread_details_are_parsed()
    {
        DiscordChannel channel = Dispatch("""
            {"id":"1","guild_id":"2","type":11,"owner_id":"9","applied_tags":["5"],
             "available_tags":[{"id":"5","name":"bug","moderated":true,"emoji_id":null,"emoji_name":"🐛"}],
             "thread_metadata":{"archived":true,"auto_archive_duration":1440,"archive_timestamp":"2026-01-01T00:00:00+00:00","locked":false}}
            """);

        Assert.Equal(9UL, channel.Owner_Id);
        Assert.Equal([5UL], channel.Applied_Tags!);
        Assert.Equal("bug", Assert.Single(channel.Available_Tags!).Name);
        Assert.True(channel.Thread_Metadata!.Archived);
        Assert.Equal(1440, channel.Thread_Metadata.Auto_Archive_Duration);
    }

    [Fact]
    public void Pin_updates_are_raised()
    {
        ChannelPinsUpdateEvent? received = null;
        _gateway.ChannelPinsUpdate += (_, e) => received = e;

        _gateway.HandleDispatch("CHANNEL_PINS_UPDATE", Json.Parse("""{"channel_id":"1","guild_id":"2","last_pin_timestamp":"2026-01-01T00:00:00+00:00"}"""));

        Assert.Equal(1UL, received!.ChannelId);
        Assert.Equal(2026, received.LastPinTimestamp!.Value.Year);
    }

    [Fact]
    public void Removing_the_last_pin_has_no_timestamp()
    {
        ChannelPinsUpdateEvent? received = null;
        _gateway.ChannelPinsUpdate += (_, e) => received = e;

        _gateway.HandleDispatch("CHANNEL_PINS_UPDATE", Json.Parse("""{"channel_id":"1","last_pin_timestamp":null}"""));

        Assert.Null(received!.LastPinTimestamp);
        Assert.Empty(_errors);
    }

    [Fact]
    public void Thread_member_changes_are_raised()
    {
        ThreadMembersUpdateEvent? received = null;
        _gateway.ThreadMembersUpdate += (_, e) => received = e;

        _gateway.HandleDispatch("THREAD_MEMBERS_UPDATE", Json.Parse("""{"id":"1","guild_id":"2","member_count":3,"added_members":[{"user_id":"7"}],"removed_member_ids":["8"]}"""));

        Assert.Equal(3, received!.MemberCount);
        Assert.Equal([7UL], received.AddedUserIds);
        Assert.Equal([8UL], received.RemovedUserIds);
    }

    [Fact]
    public void Thread_list_sync_delivers_threads()
    {
        ThreadListSyncEvent? received = null;
        _gateway.ThreadListSync += (_, e) => received = e;

        _gateway.HandleDispatch("THREAD_LIST_SYNC", Json.Parse("""{"guild_id":"2","threads":[{"id":"1","type":11,"name":"t"}],"members":[]}"""));

        Assert.Null(received!.ChannelIds);
        Assert.Equal(1UL, Assert.Single(received.Threads).Id);
    }

    [Fact]
    public void Gateway_rate_limits_report_the_guild_and_wait()
    {
        GatewayRateLimitedEvent? received = null;
        _gateway.GatewayRateLimited += (_, e) => received = e;

        _gateway.HandleDispatch("RATE_LIMITED", Json.Parse("""{"opcode":8,"retry_after":12.5,"meta":{"guild_id":"2","nonce":null}}"""));

        Assert.Equal(8, received!.Opcode);
        Assert.Equal(TimeSpan.FromSeconds(12.5), received.RetryAfter);
        Assert.Equal(2UL, received.GuildId);
    }
}

public class BotEndpointTests
{
    private const string Thread = """{"id":"1","type":11,"name":"t"}""";

    [Fact]
    public async Task Public_threads_are_created_with_an_explicit_type()
    {
        using BotFixture fixture = new();
        fixture.RespondWith(Thread);

        DiscordChannel? thread = await fixture.Bot.CreateThreadAsync(5UL, "Planning", autoArchiveMinutes: 60);

        Assert.Equal("/channels/5/threads", fixture.LastRoute);
        Assert.Contains("\"type\":11", fixture.Handler.LastBody);
        Assert.Contains("\"auto_archive_duration\":60", fixture.Handler.LastBody);
        Assert.Equal(1UL, thread!.Id);
    }

    [Fact]
    public async Task Private_threads_use_the_private_type()
    {
        using BotFixture fixture = new();
        fixture.RespondWith(Thread);

        await fixture.Bot.CreateThreadAsync(5UL, "Secret", isPrivate: true);

        Assert.Contains("\"type\":12", fixture.Handler.LastBody);
    }

    [Fact]
    public async Task Threads_can_start_from_a_message()
    {
        using BotFixture fixture = new();
        fixture.RespondWith(Thread);

        await fixture.Bot.CreateThreadFromMessageAsync(5UL, 6UL, "Discussion");

        Assert.Equal("/channels/5/messages/6/threads", fixture.LastRoute);
    }

    [Fact]
    public async Task Forum_posts_go_to_the_threads_route_with_a_nested_message()
    {
        using BotFixture fixture = new();
        fixture.RespondWith("{\"id\":\"1\",\"type\":11,\"message\":" + Json.Message + "}");

        DiscordMessage? message = await fixture.Bot.CreateForumPostAsync(5UL, "Patch notes", new MessageBuilder().WithContent("Out now").WithAppliedTags("7"));

        Assert.Equal("/channels/5/threads", fixture.LastRoute);
        JsonElement body = Json.Parse(fixture.Handler.LastBody!);
        Assert.Equal("Patch notes", body.GetProperty("name").GetString());
        Assert.Equal("Out now", body.GetProperty("message").GetProperty("content").GetString());
        Assert.Equal("7", body.GetProperty("applied_tags")[0].GetString());
        Assert.Equal("hi", message!.Content);
    }

    [Fact]
    public async Task WithForumPost_on_a_normal_send_uses_the_threads_route()
    {
        using BotFixture fixture = new();
        fixture.RespondWith("{\"id\":\"1\",\"type\":11,\"message\":" + Json.Message + "}");

        await fixture.Bot.SendMessageAsync(5UL, new MessageBuilder().WithContent("Hello").WithForumPost("Title"));

        Assert.Equal("/channels/5/threads", fixture.LastRoute);
        Assert.DoesNotContain("thread_name", fixture.Handler.LastBody);
    }

    [Fact]
    public async Task Polls_reach_the_create_message_body()
    {
        using BotFixture fixture = new();
        fixture.RespondWith(Json.Message);

        await fixture.Bot.SendMessageAsync(5UL, new MessageBuilder().WithPoll("Q", "A", "B"));

        Assert.Equal("/channels/5/messages", fixture.LastRoute);
        Assert.Contains("\"poll\":", fixture.Handler.LastBody);
    }

    [Fact]
    public async Task The_layout_flag_reaches_the_create_message_body()
    {
        using BotFixture fixture = new();
        fixture.RespondWith(Json.Message);

        await fixture.Bot.SendMessageAsync(5UL, new MessageBuilder().WithText("Hi"));

        Assert.Contains("\"flags\":32768", fixture.Handler.LastBody);
    }

    [Fact]
    public async Task Forwarding_sends_a_forward_reference_and_no_content()
    {
        using BotFixture fixture = new();
        fixture.RespondWith(Json.Message);

        await fixture.Bot.ForwardMessageAsync(9UL, 5UL, 6UL);

        Assert.Equal("/channels/9/messages", fixture.LastRoute);
        Assert.Equal("""{"message_reference":{"type":1,"message_id":"6","channel_id":"5"}}""", fixture.Handler.LastBody);
    }

    [Fact]
    public async Task Pins_are_read_across_every_page()
    {
        using BotFixture fixture = new();
        int calls = 0;
        fixture.Handler.Respond = _ =>
        {
            calls++;
            string entry = "{\"pinned_at\":\"2026-01-0" + calls + "T00:00:00+00:00\",\"message\":" + Json.Message + "}";
            string json = "{\"items\":[" + entry + "],\"has_more\":" + (calls == 1 ? "true" : "false") + "}";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        };

        IEnumerable<DiscordMessage> pins = await fixture.Bot.GetPinnedMessagesAsync("5");

        Assert.Equal(2, pins.Count());
        Assert.Equal("/channels/5/messages/pins?limit=50&before=2026-01-01T00%3A00%3A00%2B00%3A00", fixture.LastRoute);
    }

    [Fact]
    public async Task Pinning_uses_the_current_route()
    {
        using BotFixture fixture = new();

        await fixture.Bot.PinMessageAsync(5UL, 6UL);
        Assert.Equal("/channels/5/messages/pins/6", fixture.LastRoute);

        await fixture.Bot.UnpinMessageAsync(5UL, 6UL);
        Assert.Equal("/channels/5/messages/pins/6", fixture.LastRoute);
    }

    [Fact]
    public async Task Ending_a_poll_uses_the_expire_route()
    {
        using BotFixture fixture = new();
        fixture.RespondWith(Json.Message);

        await fixture.Bot.EndPollAsync(5UL, 6UL);

        Assert.Equal("/channels/5/polls/6/expire", fixture.LastRoute);
    }

    [Fact]
    public async Task Poll_voters_are_read_across_pages()
    {
        using BotFixture fixture = new();
        int calls = 0;
        fixture.Handler.Respond = _ =>
        {
            calls++;
            IEnumerable<string> users = Enumerable.Range(1, calls == 1 ? 100 : 1).Select(i => "{\"id\":\"" + i + "\",\"username\":\"u\"}");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"users\":[" + string.Join(",", users) + "]}") };
        };

        IEnumerable<DiscordUser> voters = await fixture.Bot.GetPollVotersAsync(5UL, 6UL, 1);

        Assert.Equal(101, voters.Count());
        Assert.Equal("/channels/5/polls/6/answers/1?limit=100&after=100", fixture.LastRoute);
    }

    [Fact]
    public async Task Member_search_escapes_the_query()
    {
        using BotFixture fixture = new();
        fixture.RespondWith("[" + Json.Member + "]");

        IEnumerable<DiscordMember> members = await fixture.Bot.SearchMembersAsync(5UL, "a b", 10);

        Assert.Equal("/guilds/5/members/search?query=a%20b&limit=10", fixture.LastRoute);
        Assert.Equal(5UL, Assert.Single(members).Guild.Id);
    }

    [Fact]
    public async Task Bulk_ban_sends_ids_and_the_audit_reason()
    {
        using BotFixture fixture = new();
        fixture.RespondWith("""{"banned_users":["1"],"failed_users":["2"]}""");

        BulkBanResult? result = await fixture.Bot.BulkBanAsync(5UL, [1UL, 2UL], 3600, reason: "Raid");

        Assert.Equal("/guilds/5/bulk-ban", fixture.LastRoute);
        Assert.Equal("""{"user_ids":["1","2"],"delete_message_seconds":3600}""", fixture.Handler.LastBody);
        Assert.Equal("Raid", fixture.Handler.Requests[^1].Headers.GetValues("X-Audit-Log-Reason").Single());
        Assert.Equal([1UL], result!.Banned_Users);
        Assert.Equal([2UL], result.Failed_Users);
    }

    [Fact]
    public async Task Bulk_ban_rejects_too_many_users()
    {
        using BotFixture fixture = new();

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Bot.BulkBanAsync(5UL, Enumerable.Range(1, 201).Select(i => (ulong)i)));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Bot.BulkBanAsync(5UL, []));
    }

    [Fact]
    public async Task Message_search_flattens_results()
    {
        using BotFixture fixture = new();
        fixture.RespondWith("{\"messages\":[[" + Json.Message + "]],\"total_results\":1}");

        IEnumerable<DiscordMessage> results = await fixture.Bot.SearchMessagesAsync(5UL, "hello there");

        Assert.Equal("/guilds/5/messages/search?content=hello%20there&limit=25", fixture.LastRoute);
        Assert.Equal("hi", Assert.Single(results).Content);
    }

    [Fact]
    public async Task Message_search_waits_while_Discord_is_indexing()
    {
        using BotFixture fixture = new();
        int calls = 0;
        fixture.Handler.Respond = _ =>
        {
            calls++;
            string json = calls == 1
                ? """{"message":"Index not yet available. Try again later","code":110000,"documents_indexed":0,"retry_after":0}"""
                : "{\"messages\":[[" + Json.Message + "]]}";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        };

        IEnumerable<DiscordMessage> results = await fixture.Bot.SearchMessagesAsync(5UL, "hello");

        Assert.Equal(2, calls);
        Assert.Single(results);
    }

    [Fact]
    public async Task Application_emojis_use_the_applications_route()
    {
        using BotFixture fixture = new();
        fixture.Handler.Respond = request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(request.RequestUri!.AbsolutePath.EndsWith("/@me", StringComparison.Ordinal)
                ? """{"id":"99"}"""
                : """{"items":[{"id":"1","name":"party"}]}""")
        };

        IEnumerable<DiscordEmoji> emojis = await fixture.Bot.GetApplicationEmojisAsync();

        Assert.Equal("/applications/99/emojis", fixture.LastRoute);
        Assert.Equal("party", Assert.Single(emojis).Name);
    }

    [Fact]
    public async Task Archived_threads_are_listed()
    {
        using BotFixture fixture = new();
        fixture.RespondWith("{\"threads\":[" + Thread + "],\"members\":[],\"has_more\":false}");

        IEnumerable<DiscordChannel> threads = await fixture.Bot.GetArchivedThreadsAsync(5UL, isPrivate: true);

        Assert.Equal("/channels/5/threads/archived/private", fixture.LastRoute);
        Assert.Single(threads);
    }

    [Fact]
    public void Polls_on_fetched_messages_are_parsed()
    {
        const string poll = """{"question":{"text":"Q"},"answers":[{"answer_id":1,"poll_media":{"text":"A","emoji":{"name":"🍕"}}}],"expiry":"2026-01-01T00:00:00+00:00","allow_multiselect":true,"layout_type":1,"results":{"is_finalized":true,"answer_counts":[{"id":1,"count":4,"me_voted":false}]}}""";
        string json = Json.Message.TrimEnd('}') + ",\"poll\":" + poll + "}";

        DiscordPoll parsed = JsonSerializer.Deserialize<DiscordMessage>(json, Json.Options)!.Poll!;

        Assert.Equal("Q", parsed.Question.Text);
        Assert.Equal(1, parsed.Answers[0].Answer_Id);
        Assert.Equal("🍕", parsed.Answers[0].Poll_Media.Emoji!.Name);
        Assert.True(parsed.Results!.Is_Finalized);
        Assert.Equal(4, parsed.Results.Answer_Counts[0].Count);
    }
}
