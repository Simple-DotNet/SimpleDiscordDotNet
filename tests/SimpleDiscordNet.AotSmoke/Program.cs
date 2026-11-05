using System.Net;
using System.Text.Json;
using SimpleDiscordNet;
using SimpleDiscordNet.Commands;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Gateway;
using SimpleDiscordNet.Logging;
using SimpleDiscordNet.Models;
using SimpleDiscordNet.Models.Requests;
using SimpleDiscordNet.Primitives;
using SimpleDiscordNet.Rest;

// Exercises the public API under Native AOT, where reflection-based JSON isn't available.
// Exits with a non-zero code if anything fails.

int failures = 0;

void Check(string name, Func<bool> check)
{
    try
    {
        bool passed = check();
        Console.WriteLine($"{(passed ? "PASS" : "FAIL")} {name}");
        if (!passed) failures++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAIL {name}: {ex.GetType().Name}: {ex.Message.Split('\n')[0]}");
        failures++;
    }
}

const string UserJson = """{"id":"80351110224678912","username":"Nelly","discriminator":"0","global_name":"Nell","avatar":"abc","bot":true}""";
const string RoleJson = """{"id":"41771983423143936","name":"Mods","color":3447003,"position":1,"permissions":"66321471"}""";
const string MemberJson = "{\"user\":" + UserJson + ",\"nick\":\"Nick\",\"roles\":[\"41771983423143936\"],\"joined_at\":\"2015-04-26T06:26:56+00:00\",\"permissions\":\"8\"}";
const string GuildJson = "{\"id\":\"1\",\"name\":\"g\",\"owner_id\":\"2\",\"member_count\":3,\"features\":[],\"roles\":[" + RoleJson + "],\"emojis\":[{\"id\":null,\"name\":\"x\"}]}";
const string ChannelJson = """{"id":"1","guild_id":"2","name":"general","type":0,"parent_id":"3","permission_overwrites":[{"id":"4","type":0,"allow":"1024","deny":"0"}]}""";
const string MessageJson = "{\"id\":\"1\",\"channel_id\":\"2\",\"author\":" + UserJson + ",\"content\":\"hi\",\"attachments\":[],\"embeds\":[],\"reactions\":[{\"count\":1,\"me\":false,\"emoji\":{\"id\":null,\"name\":\"x\"}}],\"components\":[{\"type\":1,\"components\":[]}]}";

JsonSerializerOptions json = new DiscordBotOptions().JsonOptions;

Check("reflection-based JSON is disabled", () => !JsonSerializer.IsReflectionEnabledByDefault);
Check("builder creates a bot", () =>
{
    using DiscordBot bot = DiscordBot.NewBuilder().WithToken("x").Build();
    return bot is not null;
});
Check("generated commands are registered", () =>
    GeneratedRegistry.Providers.Select(p => p.CreateManifest()).Any(m => m.Ungrouped.ContainsKey("hello") && m.Components.Count > 0));

Check("user", () => JsonSerializer.Deserialize<DiscordUser>(UserJson, json)!.Global_Name == "Nell");
Check("member", () => JsonSerializer.Deserialize<DiscordMember>(MemberJson, json)!.Roles.Length == 1);
Check("role", () => JsonSerializer.Deserialize<DiscordRole>(RoleJson, json)!.Color == 3447003);
Check("guild", () => JsonSerializer.Deserialize<DiscordGuild>(GuildJson, json)!.Roles!.Length == 1);
Check("channel", () => JsonSerializer.Deserialize<DiscordChannel>(ChannelJson, json)!.Permission_Overwrites!.Length == 1);
Check("message", () => JsonSerializer.Deserialize<DiscordMessage>(MessageJson, json)!.Reactions!.Length == 1);
Check("ban", () => JsonSerializer.Deserialize<DiscordBan>("{\"user\":" + UserJson + "}", json)!.user.Id != 0);
Check("audit log", () => JsonSerializer.Deserialize<DiscordAuditLog>("""{"audit_log_entries":[{"id":"1","action_type":72,"options":{"count":"5"}}]}""", json)!.audit_log_entries[0].options!.count == 5);

// RestClient serializes payloads through an object parameter, which is the riskiest path under AOT
RecordingHandler handler = new();
using RestClient rest = new(new HttpClient(handler), "token", json, new NativeLogger(), new RateLimiter());

async Task CheckPayloadAsync(string name, Func<Task> send, string expected)
{
    try
    {
        await send();
        bool passed = handler.LastBody?.Contains(expected) == true;
        Console.WriteLine($"{(passed ? "PASS" : "FAIL")} {name}: {handler.LastBody}");
        if (!passed) failures++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAIL {name}: {ex.GetType().Name}: {ex.Message.Split('\n')[0]}");
        failures++;
    }
}

CancellationToken ct = CancellationToken.None;

await CheckPayloadAsync("message with embed and button", () => rest.PostAsync("/channels/1/messages", new CreateMessageRequest
{
    content = "x",
    embeds = [new EmbedBuilder().WithTitle("t").Build()],
    components = [new ActionRow(new Button("label", "id", 1))]
}, ct), "\"custom_id\":\"id\"");
await CheckPayloadAsync("command sync", () => rest.PutGuildCommandsAsync("1", "2", [new ApplicationCommandDefinition { name = "n", type = 1, description = "d" }], ct), "\"name\":\"n\"");
await CheckPayloadAsync("interaction response", () => rest.PostInteractionCallbackAsync("1", "t", new InteractionResponse { type = 4, data = new InteractionResponseData { content = "hi" } }, ct), "\"content\":\"hi\"");
await CheckPayloadAsync("channel with overwrites", () => rest.PostAsync("/x", new CreateGuildChannelRequest { name = "n", permission_overwrites = [new ChannelPermissionOverwrite { Id = 1, Type = 0, Allow = 2, Deny = 3 }] }, ct), "\"allow\":\"2\"");
await CheckPayloadAsync("disconnect from voice", () => rest.PatchAsync("/x", new DisconnectVoiceRequest(), ct), "\"channel_id\":null");
await CheckPayloadAsync("clear timeout", () => rest.PatchAsync("/x", new ClearTimeoutRequest(), ct), "\"communication_disabled_until\":null");

await CheckPayloadAsync("poll", () => rest.PostAsync("/x", new CreateMessageRequest { poll = new MessageBuilder().WithPoll("Q", "A").Build().poll }, ct), "\"poll_media\"");
await CheckPayloadAsync("layout components", () => rest.PostAsync("/x", new CreateMessageRequest
{
    components = new MessageBuilder().WithContainer(new TextDisplay("x"), new Button("b", "c")).WithSection("s", new Thumbnail("u")).WithGallery("u").WithSeparator().Build().components
}, ct), "\"accessory\"");
await CheckPayloadAsync("attribute modal", () => new InteractionContext(rest, new InteractionCreateEvent { Id = "1", Token = "t", ApplicationId = "2", Type = InteractionType.MessageComponent }).OpenModalAsync<SmokeForm>(ct: ct), "\"custom_id\":\"Name\"");
await CheckPayloadAsync("forum post", () => rest.PostAsync("/x", new CreateForumPostRequest { name = "n", message = new ForumThreadMessage { content = "c" } }, ct), "\"message\":{");
await CheckPayloadAsync("bulk ban", () => rest.PostAsync("/x", new BulkBanRequest { user_ids = ["1"] }, ct), "\"user_ids\"");
await CheckPayloadAsync("command attributes", () => rest.PutGuildCommandsAsync("1", "2", [new ApplicationCommandDefinition { name = "n", description = "d", name_localizations = new() { ["de"] = "x" } }], ct), "\"name_localizations\"");

Check("permission names", () => $"{PermissionFlags.BanMembers | PermissionFlags.KickMembers}".Contains("BanMembers"));
Check("command choices", () => JsonSerializer.Serialize(CommandChoice.Create("Low", "low"), json).Contains("\"value\":\"low\"") && JsonSerializer.Serialize(CommandChoice.Create("One", 1), json).Contains("\"value\":1"));
Check("owner lookup", () => JsonSerializer.Deserialize<ApplicationInfo>("""{"id":"1","owner":{"id":"2"},"team":{"owner_user_id":"3","members":[{"role":"admin","user":{"id":"4"}}]}}""", json)!.Team!.Members[0].User.Id == 4);
Check("component templates", () => new ComponentIdTemplate("vote:{id}").Match("vote:7")!["id"] == "7");
Check("pin page", () => JsonSerializer.Deserialize<ChannelPinsResponse>("""{"items":[],"has_more":false}""", json)!.has_more == false);
Check("message search page", () => JsonSerializer.Deserialize<MessageSearchResponse>("""{"messages":[]}""", json)!.messages!.Length == 0);
Check("poll on a message", () => JsonSerializer.Deserialize<DiscordPoll>("""{"question":{"text":"Q"},"answers":[{"answer_id":1,"poll_media":{"text":"A"}}]}""", json)!.Answers[0].Answer_Id == 1);
Check("heartbeat", () => JsonSerializer.Serialize(new Heartbeat { d = null }, json) == """{"op":1,"d":null}""");
Check("identify", () => JsonSerializer.Serialize(new Identify { d = new IdentifyPayload { token = "T", intents = 1 } }, json).Contains("\"intents\":1"));
Check("presence", () => JsonSerializer.Serialize(new UpdatePresence { d = new UpdatePresencePayload { activities = [BotActivity.Game("x")] } }, json).Contains("\"name\":\"x\""));
Check("gateway frame", () => JsonSerializer.Deserialize<GatewayPayload>("""{"op":10,"d":{"heartbeat_interval":41250},"s":null,"t":null}""", json)!.op == 10);

Console.WriteLine(failures == 0 ? "All checks passed." : $"{failures} check(s) failed.");
return failures == 0 ? 0 : 1;

internal sealed class RecordingHandler : HttpMessageHandler
{
    public string? LastBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
    }
}

[Modal("Smoke form")]
public sealed class SmokeForm
{
    [ModalText("Name")]
    public string Name { get; set; } = "";
}

public enum SmokeColor
{
    Red,
    Green
}

public sealed class SmokeService;

public sealed class SmokeInjected(SmokeService service)
{
    [SlashCommand("injected", "Uses a service")]
    public string Injected() => service.ToString()!;
}

public sealed class SmokeCommands
{
    [Cooldown(5)]
    [RequireOwner]
    [SlashCommand("color", "Pick a color")]
    public string ColorAsync(SmokeColor color, int count = 2) => $"{color} {count}";

    [ButtonHandler("smoke:{id}")]
    public string TemplateAsync(ulong id) => id.ToString();

    [OnMemberJoined]
    public void Joined(SimpleDiscordNet.Events.MemberEvent e) { }

    [OnCommandError]
    public Task OnError(InteractionContext ctx, Exception ex) => ctx.RespondAsync("error");

    [ModalHandler]
    public Task SubmitAsync(InteractionContext ctx, SmokeForm form) => ctx.RespondAsync(form.Name);

    [SlashCommand("hello", "Say hello")]
    public Task HelloAsync(InteractionContext ctx, DiscordUser target, double amount) => ctx.RespondAsync($"{target.Username}: {amount}");

    [ButtonHandler("smoke_button")]
    public Task ClickAsync(InteractionContext ctx) => ctx.RespondAsync("clicked");
}
