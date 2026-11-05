using SimpleDiscordNet.Commands;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Models;
using Xunit;

namespace SimpleDiscordNet.Tests;

/// <summary>
/// These handlers go through the real source generator. If it mishandled a parameter type,
/// this project wouldn't compile.
/// </summary>
public sealed class GeneratedTestCommands
{
    public static object?[] Captured { get; private set; } = [];

    [SlashCommand("numbers", "numeric options")]
    public Task NumbersAsync(InteractionContext ctx, double amount, float? ratio, ulong snowflake, ulong? optional)
    {
        Captured = [amount, ratio, snowflake, optional];
        return Task.CompletedTask;
    }

    [SlashCommand("entities", "entity options")]
    public Task EntitiesAsync(InteractionContext ctx, DiscordUser user, DiscordMember member, DiscordChannel channel, DiscordRole role, Attachment file, DiscordUser? missing)
    {
        Captured = [user, member, channel, role, file, missing];
        return Task.CompletedTask;
    }

    [SlashCommand("limits", "option constraints")]
    public Task LimitsAsync(InteractionContext ctx,
        [CommandOption("name", "A name", MinLength = 2, MaxLength = 32)] string name,
        [CommandOption("age", "An age", MinValue = 13, MaxValue = 120, Required = false)] int age = 18)
    {
        Captured = [name, age];
        return Task.CompletedTask;
    }

    [UserContextMenu("Inspect")]
    public Task InspectAsync(InteractionContext ctx)
    {
        Captured = [ctx.TargetUser];
        return Task.CompletedTask;
    }
}

public class GeneratedCommandTests
{
    private static IGeneratedManifest Manifest { get; } = GeneratedRegistry.Providers
        .Select(p => p.CreateManifest())
        .Single(m => m.Ungrouped.ContainsKey("numbers"));

    private static InteractionContext Context(ApplicationCommandData data)
        => new(null!, new InteractionCreateEvent { Id = "1", Token = "t", ApplicationId = "2", Type = InteractionType.ApplicationCommand, Data = data });

    private static int OptionType(string command, string option)
        => Manifest.Definitions.Single(d => d.name == command).options!.Single(o => o.name == option).type;

    [Theory]
    [InlineData("amount", 10)]
    [InlineData("ratio", 10)]
    [InlineData("snowflake", 3)]
    public void Numeric_parameters_map_to_the_right_option_types(string option, int expected)
    {
        Assert.Equal(expected, OptionType("numbers", option));
    }

    [Theory]
    [InlineData("user", 6)]
    [InlineData("member", 6)]
    [InlineData("channel", 7)]
    [InlineData("role", 8)]
    [InlineData("file", 11)]
    public void Entity_parameters_map_to_the_right_option_types(string option, int expected)
    {
        Assert.Equal(expected, OptionType("entities", option));
    }

    [Fact]
    public async Task Fractional_numbers_and_snowflakes_reach_the_handler()
    {
        ApplicationCommandData data = new()
        {
            Name = "numbers",
            Options =
            [
                new InteractionOption { Name = "amount", Number = 3.14 },
                new InteractionOption { Name = "ratio", Number = 0.5 },
                new InteractionOption { Name = "snowflake", String = "1234567890123456789" }
            ]
        };

        await Manifest.Ungrouped["numbers"].Invoke(Context(data), CancellationToken.None);

        Assert.Equal(new object?[] { 3.14, 0.5f, 1234567890123456789UL, null }, GeneratedTestCommands.Captured);
    }

    [Fact]
    public async Task Entity_options_bind_from_resolved_data()
    {
        DiscordUser user = new() { Id = 1, Username = "u" };
        DiscordMember member = new() { User = user };
        DiscordChannel channel = new() { Id = 2, Type = 0 };
        DiscordRole role = new() { Id = 3, Name = "r" };
        Attachment file = new() { Id = "4", Filename = "a.png", Size = 1, Url = "https://x" };

        ApplicationCommandData data = new()
        {
            Name = "entities",
            Options =
            [
                new InteractionOption { Name = "user", String = "1" },
                new InteractionOption { Name = "member", String = "1" },
                new InteractionOption { Name = "channel", String = "2" },
                new InteractionOption { Name = "role", String = "3" },
                new InteractionOption { Name = "file", String = "4" }
            ],
            Resolved = new ResolvedData
            {
                Users = new() { ["1"] = user },
                Members = new() { ["1"] = member },
                Channels = new() { ["2"] = channel },
                Roles = new() { ["3"] = role },
                Attachments = new() { ["4"] = file }
            }
        };

        await Manifest.Ungrouped["entities"].Invoke(Context(data), CancellationToken.None);

        Assert.Equal(new object?[] { user, member, channel, role, file, null }, GeneratedTestCommands.Captured);
    }

    [Fact]
    public void Option_constraints_reach_the_definition()
    {
        ApplicationCommandDefinition[] options = Manifest.Definitions.Single(d => d.name == "limits").options!;
        ApplicationCommandDefinition name = options.Single(o => o.name == "name");
        ApplicationCommandDefinition age = options.Single(o => o.name == "age");

        Assert.Equal(2, name.min_length);
        Assert.Equal(32, name.max_length);
        Assert.Equal(13, age.min_value);
        Assert.Equal(120, age.max_value);
        Assert.False(age.required);
    }

    [Fact]
    public void Context_menus_have_an_empty_description()
    {
        ApplicationCommandDefinition inspect = Manifest.Definitions.Single(d => d.name == "Inspect");

        Assert.Equal(2, inspect.type);
        Assert.Equal(string.Empty, inspect.description);
    }
}
