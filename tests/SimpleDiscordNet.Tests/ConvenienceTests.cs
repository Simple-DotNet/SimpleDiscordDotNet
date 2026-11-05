using System.Net;
using System.Reflection;
using System.Text.Json;
using SimpleDiscordNet;
using SimpleDiscordNet.Commands;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Events;
using SimpleDiscordNet.Models;
using SimpleDiscordNet.Primitives;
using Xunit;

[assembly: GenerateHelp(Name = "commands", Title = "All Commands")]

namespace SimpleDiscordNet.Tests;

public enum Paint
{
    Red,
    DarkBlue,
    Green
}

public interface IGreeter
{
    string Greet();
}

public sealed class TestGreeter : IGreeter
{
    public string Greet() => "Hi from a service";
}

public sealed class TestServices : IServiceProvider
{
    public object? GetService(Type serviceType) => serviceType == typeof(IGreeter) ? new TestGreeter() : null;
}

/// <summary>Takes a service in its constructor, so generated code has to supply it.</summary>
public sealed class InjectedCommands(IGreeter greeter, IDiscordBot bot)
{
    [SlashCommand("injected", "Uses a service")]
    public string Injected() => bot is null ? "missing bot" : greeter.Greet();
}

public sealed class ConvenienceCommands
{
    public static object?[] Captured { get; private set; } = [];
    public static int Runs { get; set; }

    [SlashCommand("paint", "Pick a color")]
    public Task PaintAsync(InteractionContext ctx, Paint color, Paint? second, Paint third = Paint.Green)
    {
        Captured = [color, second, third];
        return Task.CompletedTask;
    }

    [SlashCommand("defaults", "Default values")]
    public Task DefaultsAsync(InteractionContext ctx, int count = 7, string name = "anon", bool loud = true, double ratio = 1.5, long big = 9, int? maybe = 3)
    {
        Captured = [count, name, loud, ratio, big, maybe];
        return Task.CompletedTask;
    }

    [SlashCommand("priority", "Explicit choices")]
    public void Priority([CommandChoice("Low", 1)][CommandChoice("High", 2)] int level, [CommandChoice("Quiet", "quiet")] string mode) => Captured = [level, mode];

    [SlashCommand("find", "Autocomplete")]
    public void Find([CommandOption("query", "Search", Autocomplete = true)] string query, [CommandOption("tag", "Tag", Autocomplete = true)] string tag) { }

    [Autocomplete("find", "query")]
    public IEnumerable<CommandChoice> FindQuery() => [CommandChoice.Create("One", "one")];

    [Autocomplete("find", "tag")]
    public async Task<IEnumerable<CommandChoice>> FindTagAsync(InteractionContext ctx)
    {
        await Task.Yield();
        return [CommandChoice.Create("Two", 2)];
    }

    [SlashCommand("reply-text", "Returns text")]
    public string ReplyText() => "Pong!";

    [SlashCommand("reply-async", "Returns text later")]
    public async Task<string> ReplyAsync(InteractionContext ctx)
    {
        await Task.Yield();
        return "Later";
    }

    [SlashCommand("reply-embed", "Returns an embed")]
    public EmbedBuilder ReplyEmbed() => new EmbedBuilder().WithTitle("Hello");

    [SlashCommand("reply-message", "Returns a message")]
    public ValueTask<MessageBuilder> ReplyMessage() => ValueTask.FromResult(new MessageBuilder().WithContent("Built"));

    [SlashCommand("reply-nothing", "Returns null")]
    public string? ReplyNothing() => null;

    [SlashCommand("value-task", "Returns a ValueTask")]
    public async ValueTask ValueTaskCommand()
    {
        await Task.Yield();
        Captured = ["ran"];
    }

    [Cooldown(30)]
    [SlashCommand("slow", "Has a cooldown")]
    public void Slow() => Runs++;

    [RequireOwner]
    [SlashCommand("owner-only", "Owner only")]
    public void OwnerOnly() => Runs++;

    [RequireRole("Mods", Message = "Mods only!")]
    [SlashCommand("mods-only", "Role check")]
    public void ModsOnly() => Runs++;

    [RequireBotPermissions(PermissionFlags.BanMembers | PermissionFlags.KickMembers)]
    [SlashCommand("needs-perms", "Bot permission check")]
    public void NeedsPerms() => Runs++;

    [RequireRole("Mods")]
    [Cooldown(30)]
    [SlashCommand("role-and-cooldown", "Both checks")]
    public void RoleAndCooldown() => Runs++;

    [ButtonHandler("vote:{pollId}:{choice}")]
    public Task VoteAsync(InteractionContext ctx, ulong pollId, string choice)
    {
        Captured = [pollId, choice];
        return Task.CompletedTask;
    }

    [ButtonHandler("vote:{pollId}")]
    public void VoteShort(int pollId) => Captured = [pollId];

    [ButtonHandler("paint:{color}:{big}")]
    public void PaintButton(Paint color, bool big) => Captured = [color, big];

    [Cooldown(30)]
    [ButtonHandler("limited")]
    public void Limited() => Runs++;

    [OnMemberJoined]
    public Task WelcomeAsync(MemberEvent e)
    {
        Captured = [e.User.Username];
        return Task.CompletedTask;
    }

    [OnEvent(nameof(DiscordEvents.ChannelPinsUpdated))]
    public static void PinsChanged(ChannelPinsUpdateEvent e) => Captured = [e.ChannelId];

    [OnConnected]
    public void Connected() => Runs += 100;

    [OnMemberLeft]
    public void Broken(MemberEvent e) => throw new InvalidOperationException("event failed");

    [OnChannelCreated]
    public void ChannelMade(ChannelEvent e) => Captured = [e.Channel.Name];

    [OnGuildRemoved]
    public void GuildGone(string guildId) => Captured = [guildId];

    [OnDisconnected]
    public void Dropped(Exception? reason) => Captured = [reason?.Message];

    [OnPollVoteAdded]
    [OnPollVoteRemoved]
    public void Voted() => Runs += 1000;

    public static int ErrorRuns { get; set; }

    [OnError]
    public void OnErrorEvent(Exception ex)
    {
        ErrorRuns++;
        throw new InvalidOperationException("the error handler failed too");
    }
}

public static class ErrorCommands
{
    public static Exception? Handled { get; private set; }

    [SlashCommand("explode", "Throws")]
    public static void Explode() => throw new InvalidOperationException("boom");

    [OnCommandError]
    public static async Task OnError(InteractionContext ctx, Exception ex)
    {
        Handled = ex;
        await ctx.RespondAsync("Sorry about that.");
    }
}

internal sealed class ManualClock : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UnixEpoch;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

[Collection(nameof(StaticEventsCollection))]
public class ConvenienceTests : IDisposable
{
    private readonly ManualClock _time = new();
    private readonly RestFixture _rest = new();

    private static IGeneratedManifest Manifest { get; } = GeneratedRegistry.Providers
        .Select(p => p.CreateManifest())
        .Single(m => m.Ungrouped.ContainsKey("paint"));

    public ConvenienceTests()
    {
        CommandChecks.ResetForTests();
        CommandChecks.Time = _time;
        ConvenienceCommands.Runs = 0;
    }

    public void Dispose()
    {
        CommandChecks.Time = TimeProvider.System;
        _rest.Dispose();
    }

    private static InteractionCreateEvent Event(string name, params InteractionOption[] options) => new()
    {
        Id = "1",
        Token = "t",
        ApplicationId = "2",
        GuildId = "3",
        ChannelId = "4",
        Type = InteractionType.ApplicationCommand,
        Author = new Author { Id = 55, Username = "sam" },
        Data = new ApplicationCommandData { Name = name, Options = options }
    };

    private async Task RunAsync(InteractionCreateEvent e)
    {
        SlashCommandService service = new(_rest.Logger);
        service.RegisterGeneratedManifest(Manifest);
        await service.HandleAsync(e, _rest.Rest, CancellationToken.None);
    }

    private Task RunAsync(string name, params InteractionOption[] options) => RunAsync(Event(name, options));

    private async Task ClickAsync(string customId, string? user = null)
    {
        ComponentService service = new(_rest.Logger);
        foreach (ComponentHandler handler in Manifest.Components) service.RegisterGenerated(handler);

        await service.HandleAsync(new InteractionCreateEvent
        {
            Id = "9",
            Token = "click",
            ApplicationId = "2",
            ChannelId = "4",
            Type = InteractionType.MessageComponent,
            Author = new Author { Id = user is null ? 55 : ulong.Parse(user), Username = "x" },
            Component = new MessageComponentData { CustomId = customId, ComponentType = 2 }
        }, _rest.Rest, CancellationToken.None);
    }

    private JsonElement LastBody => Json.Parse(_rest.Handler.LastBody!);

    private string? LastContent()
    {
        JsonElement body = LastBody;
        JsonElement message = body.TryGetProperty("data", out JsonElement data) ? data : body;
        return message.GetProperty("content").GetString();
    }

    private static ApplicationCommandDefinition Definition(string name) => Manifest.Definitions.Single(d => d.name == name);

    [Fact]
    public void Enums_become_string_options_with_spaced_choices()
    {
        ApplicationCommandDefinition color = Definition("paint").options![0];

        Assert.Equal(3, color.type);
        Assert.Equal(["Red", "Dark Blue", "Green"], color.choices!.Select(c => c.name));
        Assert.Equal("DarkBlue", color.choices![1].value!.Value.GetString());
        Assert.True(color.required);
    }

    [Fact]
    public void CommandChoice_values_keep_their_types()
    {
        ApplicationCommandDefinition[] options = Definition("priority").options!;

        Assert.Equal(2, options[0].choices![1].value!.Value.GetInt32());
        Assert.Equal("quiet", options[1].choices![0].value!.Value.GetString());
        Assert.Contains("\"value\":2", JsonSerializer.Serialize(options[0], Json.Options));
    }

    [Theory]
    [InlineData("find:query", "One")]
    [InlineData("find:tag", "Two")]
    public async Task Autocomplete_handlers_can_be_sync_or_async(string key, string expected)
    {
        IEnumerable<CommandChoice> choices = await Manifest.AutocompleteHandlers[key].Invoke(new InteractionContext(_rest.Rest, Event("find")), CancellationToken.None);

        Assert.Equal(expected, Assert.Single(choices).name);
    }

    [Fact]
    public async Task Enum_options_reach_the_handler()
    {
        await RunAsync("paint", new InteractionOption { Name = "color", String = "DarkBlue" });

        Assert.Equal(new object?[] { Paint.DarkBlue, null, Paint.Green }, ConvenienceCommands.Captured);
    }

    [Fact]
    public async Task Default_values_apply_to_omitted_options()
    {
        await RunAsync("defaults");

        Assert.Equal(new object?[] { 7, "anon", true, 1.5, 9L, 3 }, ConvenienceCommands.Captured);
    }

    [Fact]
    public async Task Supplied_options_beat_default_values()
    {
        await RunAsync("defaults",
            new InteractionOption { Name = "count", Integer = 2 },
            new InteractionOption { Name = "name", String = "sam" });

        Assert.Equal(2, ConvenienceCommands.Captured[0]);
        Assert.Equal("sam", ConvenienceCommands.Captured[1]);
    }

    [Theory]
    [InlineData("reply-text", "Pong!")]
    [InlineData("reply-async", "Later")]
    [InlineData("reply-message", "Built")]
    public async Task Returned_values_become_the_reply(string command, string expected)
    {
        await RunAsync(command);

        Assert.Equal(expected, LastContent());
    }

    [Fact]
    public async Task Returned_embeds_become_the_reply()
    {
        await RunAsync("reply-embed");

        Assert.Equal("Hello", LastBody.GetProperty("embeds")[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task A_null_return_sends_nothing_extra()
    {
        await RunAsync("reply-nothing");

        JsonElement defer = LastBody;
        Assert.Equal(5, defer.GetProperty("type").GetInt32());
    }

    [Fact]
    public async Task ValueTask_handlers_are_awaited()
    {
        await RunAsync("value-task");

        Assert.Equal("ran", ConvenienceCommands.Captured[0]);
    }

    [Fact]
    public async Task A_cooldown_blocks_repeat_use_until_it_ends()
    {
        await RunAsync("slow");
        await RunAsync("slow");

        Assert.Equal(1, ConvenienceCommands.Runs);
        Assert.Equal("⏳ Try again in 30 seconds.", LastContent());
        Assert.Equal(64, LastBody.GetProperty("data").GetProperty("flags").GetInt32());

        _time.Advance(TimeSpan.FromSeconds(31));
        await RunAsync("slow");

        Assert.Equal(2, ConvenienceCommands.Runs);
    }

    [Fact]
    public async Task Cooldowns_are_per_user_by_default()
    {
        await RunAsync("slow");

        await RunAsync(Event("slow") with { Author = new Author { Id = 56, Username = "other" } });

        Assert.Equal(2, ConvenienceCommands.Runs);
    }

    [Fact]
    public async Task A_cooldown_covers_a_button()
    {
        await ClickAsync("limited");
        await ClickAsync("limited");

        Assert.Equal(1, ConvenienceCommands.Runs);
    }

    [Fact]
    public async Task Failing_a_check_doesnt_use_up_a_cooldown()
    {
        await RunAsync("role-and-cooldown");

        DiscordRole mods = new() { Id = 7, Name = "Mods" };
        DiscordGuild guild = new() { Id = 3, Name = "g", Roles = [mods] };
        DiscordMember member = new() { User = new DiscordUser { Id = 55, Username = "sam" }, Roles = [7] };
        await RunAsync(Event("role-and-cooldown") with { Member = member, Guild = guild });

        Assert.Equal(1, ConvenienceCommands.Runs);
    }

    [Fact]
    public async Task Role_checks_match_names_and_use_custom_messages()
    {
        DiscordMember outsider = new() { User = new DiscordUser { Id = 55, Username = "sam" }, Roles = [] };
        DiscordGuild guild = new() { Id = 3, Name = "g", Roles = [new DiscordRole { Id = 7, Name = "Mods" }] };

        await RunAsync(Event("mods-only") with { Member = outsider, Guild = guild });

        Assert.Equal(0, ConvenienceCommands.Runs);
        Assert.Equal("Mods only!", LastContent());
    }

    [Fact]
    public async Task Bot_permission_checks_name_whats_missing()
    {
        await RunAsync(Event("needs-perms") with { AppPermissions = (ulong)PermissionFlags.KickMembers });

        Assert.Equal(0, ConvenienceCommands.Runs);
        Assert.Contains("BanMembers", LastContent());
        Assert.DoesNotContain("KickMembers", LastContent());
    }

    [Theory]
    [InlineData(PermissionFlags.BanMembers | PermissionFlags.KickMembers)]
    [InlineData(PermissionFlags.Administrator)]
    public async Task Bot_permission_checks_pass_with_the_permissions(PermissionFlags granted)
    {
        await RunAsync(Event("needs-perms") with { AppPermissions = (ulong)granted });

        Assert.Equal(1, ConvenienceCommands.Runs);
    }

    [Fact]
    public async Task Owner_checks_use_the_applications_owner()
    {
        _rest.Handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"id":"2","owner":{"id":"55"}}""") };

        await RunAsync("owner-only");
        await RunAsync(Event("owner-only") with { Author = new Author { Id = 56, Username = "other" } });

        Assert.Equal(1, ConvenienceCommands.Runs);
    }

    [Fact]
    public async Task Owner_checks_skip_read_only_team_members()
    {
        _rest.Handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"2","owner":{"id":"1"},"team":{"owner_user_id":"70","members":[{"role":"developer","user":{"id":"55"}},{"role":"read_only","user":{"id":"56"}}]}}""")
        };

        await RunAsync("owner-only");
        await RunAsync(Event("owner-only") with { Author = new Author { Id = 56, Username = "other" } });
        await RunAsync(Event("owner-only") with { Author = new Author { Id = 70, Username = "boss" } });

        Assert.Equal(2, ConvenienceCommands.Runs);
    }

    [Fact]
    public async Task Component_ID_parts_bind_to_parameters()
    {
        await ClickAsync("vote:123456789012345678:yes");

        Assert.Equal(new object?[] { 123456789012345678UL, "yes" }, ConvenienceCommands.Captured);
    }

    [Fact]
    public async Task The_template_with_the_right_shape_wins()
    {
        await ClickAsync("vote:42");

        Assert.Equal(new object?[] { 42 }, ConvenienceCommands.Captured);
    }

    [Fact]
    public async Task Component_ID_parts_support_enums_and_bools()
    {
        await ClickAsync("paint:DarkBlue:true");

        Assert.Equal(new object?[] { Paint.DarkBlue, true }, ConvenienceCommands.Captured);
    }

    [Theory]
    [InlineData("a:{x}", "a:1", "1")]
    [InlineData("a:{x}:end", "a:1:end", "1")]
    [InlineData("{x}-suffix", "1-suffix", "1")]
    public void Templates_capture_their_parts(string template, string id, string expected)
    {
        Assert.Equal(expected, new ComponentIdTemplate(template).Match(id)!["x"]);
    }

    [Theory]
    [InlineData("a:{x}", "b:1")]
    [InlineData("a:{x}", "a:")]
    [InlineData("a:{x}:end", "a:1:other")]
    [InlineData("a:{x}:{y}", "a:1")]
    public void Templates_reject_IDs_of_another_shape(string template, string id)
    {
        Assert.Null(new ComponentIdTemplate(template).Match(id));
    }

    [Fact]
    public async Task Confirm_returns_true_when_the_user_clicks_yes()
    {
        _rest.Handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Json.Message) };
        InteractionContext ctx = new(_rest.Rest, Event("x"));

        Task<bool> answer = ctx.ConfirmAsync("Sure?");
        string yes = await FirstButtonAsync();

        await ClickAsync(yes);

        Assert.True(await answer);
        JsonElement update = LastBody;
        Assert.Equal(7, update.GetProperty("type").GetInt32());
        Assert.Equal(0, update.GetProperty("data").GetProperty("components").GetArrayLength());
    }

    [Fact]
    public async Task Confirm_returns_false_for_no()
    {
        _rest.Handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Json.Message) };
        InteractionContext ctx = new(_rest.Rest, Event("x"));

        Task<bool> answer = ctx.ConfirmAsync("Sure?");
        await ClickAsync(await ButtonAsync(1));

        Assert.False(await answer);
    }

    [Fact]
    public async Task Other_people_cant_answer()
    {
        _rest.Handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Json.Message) };
        InteractionContext ctx = new(_rest.Rest, Event("x"));

        Task<string?> answer = ctx.ChooseAsync("Pick", ["A", "B"]);
        string first = await FirstButtonAsync();

        await ClickAsync(first, user: "99");
        await WaitForAsync(() => _rest.Handler.Requests.Count >= 2);
        Assert.Equal(64, LastBody.GetProperty("data").GetProperty("flags").GetInt32());
        Assert.False(answer.IsCompleted);

        await ClickAsync(first);
        Assert.Equal("A", await answer);
    }

    [Fact]
    public async Task An_unanswered_question_times_out_and_removes_its_buttons()
    {
        _rest.Handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Json.Message) };
        InteractionContext ctx = new(_rest.Rest, Event("x"));

        bool answer = await ctx.ConfirmAsync("Sure?", TimeSpan.FromMilliseconds(30));

        Assert.False(answer);
        Assert.Equal(HttpMethod.Patch, _rest.Handler.Requests[^1].Method);
        Assert.Equal(0, LastBody.GetProperty("components").GetArrayLength());
    }

    [Fact]
    public async Task Clicking_an_old_button_says_it_expired()
    {
        await ClickAsync("sdn~gone~0");

        Assert.Contains("expired", LastContent());
    }

    [Fact]
    public async Task Pagination_moves_between_pages_and_disables_the_ends()
    {
        _rest.Handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Json.Message) };
        InteractionContext ctx = new(_rest.Rest, Event("x"));

        Task paging = ctx.PaginateAsync(["One", "Two"], TimeSpan.FromMilliseconds(400));

        string next = await ButtonAsync(2);
        JsonElement firstButtons = LastBody.GetProperty("data").GetProperty("components")[0].GetProperty("components");
        Assert.True(firstButtons[0].GetProperty("disabled").GetBoolean());
        Assert.Equal("1 / 2", firstButtons[1].GetProperty("label").GetString());

        await ClickAsync(next);
        await WaitForAsync(() => _rest.Handler.Requests.Count >= 2);

        JsonElement update = LastBody;
        Assert.Equal("Two", update.GetProperty("data").GetProperty("content").GetString());
        Assert.True(update.GetProperty("data").GetProperty("components")[0].GetProperty("components")[2].GetProperty("disabled").GetBoolean());

        await paging;
        Assert.Equal(HttpMethod.Patch, _rest.Handler.Requests[^1].Method);
    }

    [Fact]
    public async Task A_single_page_has_no_buttons()
    {
        InteractionContext ctx = new(_rest.Rest, Event("x"));

        await ctx.PaginateAsync(["Only"]);

        Assert.False(LastBody.GetProperty("data").TryGetProperty("components", out _));
    }

    private async Task<string> ButtonAsync(int index)
    {
        await WaitForAsync(() => _rest.Handler.Requests.Count >= 1);
        return LastBody.GetProperty("data").GetProperty("components")[0].GetProperty("components")[index].GetProperty("custom_id").GetString()!;
    }

    private Task<string> FirstButtonAsync() => ButtonAsync(0);

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (int i = 0; i < 200 && !condition(); i++) await Task.Delay(10);
        Assert.True(condition());
    }

    [Fact]
    public async Task Event_attributes_subscribe_to_events()
    {
        DiscordEvents.RaiseMemberJoined(null, new MemberEvent
        {
            Member = new DiscordMember { User = new DiscordUser { Id = 1, Username = "newbie" } },
            User = new DiscordUser { Id = 1, Username = "newbie" },
            Guild = new DiscordGuild { Id = 2, Name = "g" }
        });

        await WaitForAsync(() => ConvenienceCommands.Captured is ["newbie"]);
    }

    [Fact]
    public async Task OnEvent_works_with_static_handlers()
    {
        DiscordEvents.RaiseChannelPinsUpdated(null, new ChannelPinsUpdateEvent { ChannelId = 77 });

        await WaitForAsync(() => ConvenienceCommands.Captured is [77UL]);
    }

    [Fact]
    public async Task Handlers_without_a_parameter_work()
    {
        DiscordEvents.RaiseConnected(null);

        await WaitForAsync(() => ConvenienceCommands.Runs >= 100);
    }

    [Fact]
    public async Task Exceptions_in_event_handlers_go_to_the_error_event()
    {
        Exception? received = null;
        void OnError(object? sender, Exception ex) => received = ex;
        DiscordEvents.Error += OnError;
        try
        {
            DiscordEvents.RaiseMemberLeft(null, new MemberEvent
            {
                Member = new DiscordMember { User = new DiscordUser { Id = 1, Username = "u" } },
                User = new DiscordUser { Id = 1, Username = "u" },
                Guild = new DiscordGuild { Id = 2, Name = "g" }
            });

            await WaitForAsync(() => received is not null);
            Assert.Equal("event failed", received!.Message);
        }
        finally
        {
            DiscordEvents.Error -= OnError;
        }
    }

    [Fact]
    public void Every_shortcut_attribute_is_named_after_its_event()
    {
        // The source generator works out the event from the attribute's name, so these have to match
        Type[] shortcuts = typeof(OnEventAttribute).Assembly.GetExportedTypes()
            .Where(t => t.IsSubclassOf(typeof(OnEventAttribute)))
            .ToArray();

        Assert.True(shortcuts.Length > 40);
        foreach (Type shortcut in shortcuts)
        {
            OnEventAttribute attribute = (OnEventAttribute)Activator.CreateInstance(shortcut)!;
            string expected = attribute.EventName == nameof(DiscordEvents.DirectMessageReceived)
                ? "OnDirectMessageAttribute"
                : $"On{attribute.EventName}Attribute";

            Assert.Equal(expected, shortcut.Name);
        }
    }

    [Fact]
    public async Task Shortcuts_pass_along_event_data()
    {
        DiscordEvents.RaiseChannelCreated(null, new ChannelEvent
        {
            Channel = new DiscordChannel { Id = 1, Name = "general", Type = 0 },
            Guild = new DiscordGuild { Id = 2, Name = "g" }
        });

        await WaitForAsync(() => ConvenienceCommands.Captured is ["general"]);
    }

    [Fact]
    public async Task Shortcuts_work_with_events_that_send_a_string()
    {
        DiscordEvents.RaiseGuildRemoved(null, "123");

        await WaitForAsync(() => ConvenienceCommands.Captured is ["123"]);
    }

    [Fact]
    public async Task One_method_can_handle_several_events()
    {
        PollVoteAddedEvent vote = new() { ChannelId = 1, MessageId = 2, UserId = 3, AnswerId = 4 };
        PollVoteRemovedEvent unvote = new() { ChannelId = 1, MessageId = 2, UserId = 3, AnswerId = 4 };

        DiscordEvents.RaisePollVoteAdded(null, vote);
        DiscordEvents.RaisePollVoteRemoved(null, unvote);

        await WaitForAsync(() => ConvenienceCommands.Runs >= 2000);
    }

    [Fact]
    public async Task A_failing_error_handler_doesnt_loop()
    {
        ConvenienceCommands.ErrorRuns = 0;

        DiscordEvents.RaiseError(null, new InvalidOperationException("boom"));

        await WaitForAsync(() => ConvenienceCommands.ErrorRuns >= 1);
        await Task.Delay(100);
        Assert.Equal(1, ConvenienceCommands.ErrorRuns);
    }

    [Fact]
    public async Task OnCommandError_replaces_the_default_message()
    {
        await RunAsync("explode");

        Assert.Equal("boom", ErrorCommands.Handled!.Message);
        Assert.Equal("Sorry about that.", LastContent());
    }

    [Fact]
    public async Task Handler_classes_receive_services()
    {
        using DiscordBot bot = DiscordBot.NewBuilder().WithToken("x").WithServices(new TestServices()).Build();

        await RunAsync("injected");

        Assert.Equal("Hi from a service", LastContent());
    }

    [Fact]
    public void Missing_services_give_a_helpful_error()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => HandlerServices.Get<ICloneable>());

        Assert.Contains("WithServices", error.Message);
    }

    [Fact]
    public void Bots_built_from_options_register_their_commands()
    {
        using DiscordBot bot = DiscordBot.FromOptions(new DiscordBotOptions { Token = "x" });

        object? manifests = typeof(DiscordBot).GetField("_generatedManifests", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bot);

        Assert.NotEmpty(Assert.IsAssignableFrom<IEnumerable<IGeneratedManifest>>(manifests));
    }

    [Fact]
    public void GenerateHelp_adds_a_command_with_its_settings()
    {
        Assert.Equal("List the bot's commands", Definition("commands").description);
        Assert.True(Manifest.Ungrouped["commands"].DeferEphemeral);
    }

    [Fact]
    public async Task The_help_command_lists_commands_sorted()
    {
        await RunAsync("commands");

        JsonElement embed = LastBody.GetProperty("embeds")[0];
        Assert.Equal("All Commands", embed.GetProperty("title").GetString());
        string text = embed.GetProperty("description").GetString()!;
        Assert.Contains("`/paint` - Pick a color", text);
        Assert.True(text.IndexOf("/defaults", StringComparison.Ordinal) < text.IndexOf("/paint", StringComparison.Ordinal));
    }

    [Fact]
    public void Long_help_lists_split_into_pages()
    {
        Dictionary<string, string> commands = Enumerable.Range(0, 200).ToDictionary(i => $"/command{i}", _ => new string('x', 50));

        List<EmbedBuilder> pages = HelpFormatter.BuildPages(commands, "Commands");

        Assert.True(pages.Count > 1);
    }
}
