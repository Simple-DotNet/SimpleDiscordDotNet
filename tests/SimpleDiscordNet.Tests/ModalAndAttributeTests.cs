using System.Text.Json;
using SimpleDiscordNet.Commands;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Gateway;
using SimpleDiscordNet.Models;
using SimpleDiscordNet.Primitives;
using Xunit;

namespace SimpleDiscordNet.Tests;

[Modal("Send feedback")]
public sealed class FeedbackForm
{
    [ModalText("Your name", MaxLength = 50, Placeholder = "Sam", Description = "So we can reply")]
    public string Name { get; set; } = "";

    [ModalText("Details", Paragraph = true)]
    public string? Details { get; set; }

    [ModalSelect("Area", "Bug report:bug", "Feature request:feature")]
    public string Area { get; set; } = "";

    [ModalCheckbox("Email me")]
    public bool Subscribe { get; set; }

    [ModalFile("Screenshots", FileTypes = ["image"])]
    public Attachment[] Files { get; set; } = [];
}

[Modal("Pick things", Id = "picker")]
public sealed class PickerForm
{
    [ModalUserSelect("Who")]
    public ulong User { get; set; }

    [ModalRoleSelect("Roles", MaxValues = 3)]
    public ulong[] Roles { get; set; } = [];

    [ModalChannelSelect("Where")]
    public ulong? Channel { get; set; }

    [ModalRadio("Size", "Small", "Large")]
    public string Size { get; set; } = "";

    [ModalCheckboxes("Extras", "Cheese", "Olives")]
    public string[]? Extras { get; set; }
}

public sealed class AttributeTestCommands
{
    public static FeedbackForm? LastFeedback { get; private set; }
    public static PickerForm? LastPicker { get; private set; }

    [ModalHandler]
    public Task OnFeedback(InteractionContext ctx, FeedbackForm form)
    {
        LastFeedback = form;
        return Task.CompletedTask;
    }

    [ModalHandler]
    public static void OnPicker(PickerForm form) => LastPicker = form;

    [GuildOnly]
    [SlashCommand("server-only", "Only in servers")]
    public Task ServerOnlyAsync(InteractionContext ctx) => Task.CompletedTask;

    [AllowInDMs]
    [SlashCommand("dms-too", "Also in DMs")]
    public Task DmsTooAsync(InteractionContext ctx) => Task.CompletedTask;

    [UserInstallable]
    [Localized("de", "Hallo", "Sag hallo")]
    [Localized("fr", "bonjour")]
    [SlashCommand("anywhere", "Works everywhere")]
    public Task AnywhereAsync(InteractionContext ctx, [Localized("de", "name", "Der Name")] string name) => Task.CompletedTask;

    [UserInstallable]
    [Localized("de", "Info anzeigen")]
    [UserContextMenu("Show Info")]
    public Task ShowInfoAsync(InteractionContext ctx) => Task.CompletedTask;
}

[GuildOnly]
[Localized("de", "verwaltung", "Verwaltung")]
[SlashCommandGroup("admin-tools", "Admin tools")]
public sealed class GroupedAttributeCommands
{
    [SlashCommand("reload", "Reload")]
    public Task ReloadAsync(InteractionContext ctx) => Task.CompletedTask;
}

public class CommandAttributeTests
{
    private static IGeneratedManifest Manifest { get; } = GeneratedRegistry.Providers
        .Select(p => p.CreateManifest())
        .Single(m => m.Ungrouped.ContainsKey("anywhere"));

    private static ApplicationCommandDefinition Definition(string name) => Manifest.Definitions.Single(d => d.name == name);

    [Fact]
    public void Commands_without_an_attribute_leave_availability_to_Discord()
    {
        Assert.Null(Definition("numbers").contexts);
        Assert.Null(Definition("numbers").integration_types);
    }

    [Fact]
    public void GuildOnly_limits_the_command_to_servers()
    {
        Assert.Equal([0], Definition("server-only").contexts!);
    }

    [Fact]
    public void AllowInDMs_adds_bot_DMs()
    {
        Assert.Equal([0, 1], Definition("dms-too").contexts!);
    }

    [Fact]
    public void UserInstallable_covers_every_install_and_context()
    {
        Assert.Equal([0, 1, 2], Definition("anywhere").contexts!);
        Assert.Equal([0, 1], Definition("anywhere").integration_types!);
    }

    [Fact]
    public void Context_menus_support_availability()
    {
        Assert.Equal([0, 1], Definition("Show Info").integration_types!);
    }

    [Fact]
    public void Class_attributes_apply_to_a_command_group()
    {
        ApplicationCommandDefinition group = Definition("admin-tools");

        Assert.Equal([0], group.contexts!);
        Assert.Equal("verwaltung", group.name_localizations!["de"]);
        Assert.Equal("Verwaltung", group.description_localizations!["de"]);
    }

    [Fact]
    public void Localized_names_and_descriptions_are_registered()
    {
        ApplicationCommandDefinition command = Definition("anywhere");

        Assert.Equal("hallo", command.name_localizations!["de"]);
        Assert.Equal("bonjour", command.name_localizations!["fr"]);
        Assert.Equal("Sag hallo", command.description_localizations!["de"]);
        Assert.False(command.description_localizations.ContainsKey("fr"));
    }

    [Fact]
    public void Localized_options_are_registered()
    {
        ApplicationCommandDefinition option = Assert.Single(Definition("anywhere").options!);

        Assert.Equal("Der Name", option.description_localizations!["de"]);
    }

    [Fact]
    public void Context_menu_names_keep_their_capitals_and_have_no_descriptions()
    {
        ApplicationCommandDefinition command = Definition("Show Info");

        Assert.Equal("Info anzeigen", command.name_localizations!["de"]);
        Assert.Null(command.description_localizations);
    }

    [Fact]
    public void Guild_registration_strips_global_only_fields()
    {
        ApplicationCommandDefinition original = Definition("anywhere");

        ApplicationCommandDefinition guild = original.ForGuild();

        Assert.Null(guild.contexts);
        Assert.Null(guild.integration_types);
        Assert.NotNull(guild.name_localizations);
        Assert.NotNull(original.contexts);
    }

    [Fact]
    public void Commands_serialize_with_Discords_field_names()
    {
        string json = JsonSerializer.Serialize(Definition("anywhere"), Json.Options);

        Assert.Contains("\"integration_types\":[0,1]", json);
        Assert.Contains("\"contexts\":[0,1,2]", json);
        Assert.Contains("\"name_localizations\":{", json);
        Assert.Contains("\"description_localizations\":{", json);
    }
}

public class ModalFormTests : IDisposable
{
    private readonly GatewayClient _gateway = new("token", DiscordIntents.Guilds, Json.Options);

    public void Dispose() => _gateway.Dispose();

    private static IGeneratedManifest Manifest { get; } = GeneratedRegistry.Providers
        .Select(p => p.CreateManifest())
        .Single(m => m.Ungrouped.ContainsKey("anywhere"));

    private InteractionCreateEvent Submit(string data)
    {
        InteractionCreateEvent? received = null;
        _gateway.InteractionCreate += (_, e) => received = e;
        _gateway.HandleDispatch("INTERACTION_CREATE", Json.Parse(
            "{\"id\":\"1\",\"token\":\"t\",\"application_id\":\"2\",\"type\":5,\"guild_id\":\"3\",\"channel_id\":\"4\",\"member\":" + Json.Member + ",\"data\":" + data + "}"));
        return received!;
    }

    private static string Field(string id, int type, string payload)
        => "{\"type\":18,\"component\":{\"type\":" + type + ",\"custom_id\":\"" + id + "\"," + payload + "}}";

    private static async Task<JsonElement> OpenAsync<TForm>(TForm? initial = null) where TForm : class
    {
        using RestFixture fixture = new();
        InteractionContext ctx = new(fixture.Rest, new InteractionCreateEvent { Id = "1", Token = "t", ApplicationId = "2", Type = InteractionType.MessageComponent });
        await ctx.OpenModalAsync(initial);
        return Json.Parse(fixture.Handler.LastBody!);
    }

    [Fact]
    public async Task Forms_open_with_labels_around_their_fields()
    {
        JsonElement body = await OpenAsync<FeedbackForm>();

        Assert.Equal(9, body.GetProperty("type").GetInt32());
        JsonElement data = body.GetProperty("data");
        Assert.Equal("FeedbackForm", data.GetProperty("custom_id").GetString());
        Assert.Equal("Send feedback", data.GetProperty("title").GetString());

        JsonElement name = data.GetProperty("components")[0];
        Assert.Equal(18, name.GetProperty("type").GetInt32());
        Assert.Equal("Your name", name.GetProperty("label").GetString());
        Assert.Equal("So we can reply", name.GetProperty("description").GetString());

        JsonElement input = name.GetProperty("component");
        Assert.Equal(4, input.GetProperty("type").GetInt32());
        Assert.Equal("Name", input.GetProperty("custom_id").GetString());
        Assert.Equal(1, input.GetProperty("style").GetInt32());
        Assert.Equal(50, input.GetProperty("max_length").GetInt32());
        Assert.Equal("Sam", input.GetProperty("placeholder").GetString());
        Assert.True(input.GetProperty("required").GetBoolean());
        Assert.False(input.TryGetProperty("label", out _));
    }

    [Fact]
    public async Task Form_fields_follow_their_attributes()
    {
        JsonElement components = (await OpenAsync<FeedbackForm>()).GetProperty("data").GetProperty("components");

        JsonElement details = components[1].GetProperty("component");
        Assert.Equal(2, details.GetProperty("style").GetInt32());
        Assert.False(details.GetProperty("required").GetBoolean());

        JsonElement area = components[2].GetProperty("component");
        Assert.Equal(3, area.GetProperty("type").GetInt32());
        Assert.Equal("Bug report", area.GetProperty("options")[0].GetProperty("label").GetString());
        Assert.Equal("bug", area.GetProperty("options")[0].GetProperty("value").GetString());

        Assert.Equal(23, components[3].GetProperty("component").GetProperty("type").GetInt32());

        JsonElement files = components[4].GetProperty("component");
        Assert.Equal(19, files.GetProperty("type").GetInt32());
        Assert.Equal(10, files.GetProperty("max_values").GetInt32());
        Assert.Equal("image", files.GetProperty("file_types")[0].GetString());
    }

    [Fact]
    public async Task Forms_can_be_prefilled()
    {
        JsonElement components = (await OpenAsync(new FeedbackForm { Name = "Sam", Subscribe = true })).GetProperty("data").GetProperty("components");

        Assert.Equal("Sam", components[0].GetProperty("component").GetProperty("value").GetString());
        Assert.True(components[3].GetProperty("component").GetProperty("default").GetBoolean());
    }

    [Fact]
    public async Task Picker_fields_use_their_component_types_and_custom_id()
    {
        JsonElement data = (await OpenAsync<PickerForm>()).GetProperty("data");
        JsonElement components = data.GetProperty("components");

        Assert.Equal("picker", data.GetProperty("custom_id").GetString());
        Assert.Equal(5, components[0].GetProperty("component").GetProperty("type").GetInt32());
        Assert.Equal(6, components[1].GetProperty("component").GetProperty("type").GetInt32());
        Assert.Equal(3, components[1].GetProperty("component").GetProperty("max_values").GetInt32());
        Assert.Equal(8, components[2].GetProperty("component").GetProperty("type").GetInt32());
        Assert.False(components[2].GetProperty("component").GetProperty("required").GetBoolean());
        Assert.Equal(21, components[3].GetProperty("component").GetProperty("type").GetInt32());
        Assert.Equal(22, components[4].GetProperty("component").GetProperty("type").GetInt32());
    }

    [Fact]
    public async Task A_class_without_Modal_gives_a_helpful_error()
    {
        using RestFixture fixture = new();
        InteractionContext ctx = new(fixture.Rest, new InteractionCreateEvent { Id = "1", Token = "t", ApplicationId = "2", Type = InteractionType.MessageComponent });

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.OpenModalAsync<ModalFormTests>());

        Assert.Contains("[Modal]", error.Message);
    }

    [Fact]
    public async Task Submitted_forms_are_filled_and_passed_to_the_handler()
    {
        const string attachment = """{"id":"50","filename":"a.png","size":1,"url":"https://x/a.png"}""";
        InteractionCreateEvent e = Submit("{\"custom_id\":\"FeedbackForm\",\"components\":["
            + Field("Name", 4, "\"value\":\"Sam\"") + ","
            + Field("Details", 4, "\"value\":\"\"") + ","
            + Field("Area", 3, "\"values\":[\"bug\"]") + ","
            + Field("Subscribe", 23, "\"value\":true") + ","
            + Field("Files", 19, "\"values\":[\"50\"]")
            + "],\"resolved\":{\"attachments\":{\"50\":" + attachment + "}}}");

        ComponentHandler handler = Manifest.Components.Single(c => c.Id == "FeedbackForm");
        await handler.Invoke(new InteractionContext(null!, e), CancellationToken.None);

        FeedbackForm form = AttributeTestCommands.LastFeedback!;
        Assert.Equal("Sam", form.Name);
        Assert.Equal("bug", form.Area);
        Assert.True(form.Subscribe);
        Assert.Equal("a.png", Assert.Single(form.Files).Filename);
    }

    [Fact]
    public async Task Pickers_bind_to_ids_and_static_handlers_work()
    {
        InteractionCreateEvent e = Submit("{\"custom_id\":\"picker\",\"components\":["
            + Field("User", 5, "\"values\":[\"7\"]") + ","
            + Field("Roles", 6, "\"values\":[\"8\",\"9\"]") + ","
            + Field("Channel", 8, "\"values\":[]") + ","
            + Field("Size", 21, "\"value\":\"Large\"") + ","
            + Field("Extras", 22, "\"values\":[\"Cheese\"]")
            + "]}");

        ComponentHandler handler = Manifest.Components.Single(c => c.Id == "picker");
        await handler.Invoke(new InteractionContext(null!, e), CancellationToken.None);

        PickerForm form = AttributeTestCommands.LastPicker!;
        Assert.Equal(7UL, form.User);
        Assert.Equal([8UL, 9UL], form.Roles);
        Assert.Null(form.Channel);
        Assert.Equal("Large", form.Size);
        Assert.Equal(["Cheese"], form.Extras!);
    }

    [Fact]
    public void Older_modals_built_from_action_rows_still_parse()
    {
        InteractionCreateEvent e = Submit("""{"custom_id":"old","components":[{"type":1,"components":[{"type":4,"custom_id":"feedback","value":"Great"}]}]}""");

        Assert.Equal("Great", new InteractionContext(null!, e).GetModalValue("feedback"));
    }

    [Fact]
    public void Unticked_checkboxes_read_as_false()
    {
        InteractionCreateEvent e = Submit("{\"custom_id\":\"x\",\"components\":[" + Field("Subscribe", 23, "\"value\":false") + "]}");

        Assert.False(new InteractionContext(null!, e).GetModalBool("Subscribe"));
    }
}
