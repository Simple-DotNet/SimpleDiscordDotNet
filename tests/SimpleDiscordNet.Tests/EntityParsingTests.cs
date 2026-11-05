using System.Text.Json;
using SimpleDiscordNet.Entities;
using Xunit;

namespace SimpleDiscordNet.Tests;

public class EntityParsingTests
{
    private static T Parse<T>(string json) => JsonSerializer.Deserialize<T>(json, Json.Options)!;

    [Fact]
    public void User_populates_all_fields()
    {
        DiscordUser user = Parse<DiscordUser>(Json.User);

        Assert.Equal(80351110224678912UL, user.Id);
        Assert.Equal("Nell", user.Global_Name);
        Assert.Equal("8342729096ea3675442027381ff50dfe", user.Avatar);
        Assert.True(user.Bot);
        Assert.Equal(64, user.Public_Flags);
        Assert.Equal(16711680, user.Accent_Color);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("1337", 1337)]
    public void User_discriminator_is_read_from_a_string(string discriminator, int expected)
    {
        DiscordUser user = Parse<DiscordUser>($$"""{"id":"1","username":"u","discriminator":"{{discriminator}}"}""");

        Assert.Equal(expected, user.Discriminator);
    }

    [Fact]
    public void Member_populates_all_fields()
    {
        DiscordMember member = Parse<DiscordMember>(Json.Member);

        Assert.Equal("Nick", member.Nick);
        Assert.Equal([41771983423143936UL], member.Roles);
        Assert.Equal("2015-04-26T06:26:56.936000+00:00", member.Joined_At);
        Assert.Equal(8UL, member.Permissions);
        Assert.False(member.Deaf);
    }

    [Fact]
    public void Role_parses_without_a_guild_in_the_payload()
    {
        DiscordRole role = Parse<DiscordRole>(Json.Role);

        Assert.Equal(3447003, role.Color);
        Assert.Equal(66321471UL, role.Permissions);
        Assert.Equal(1, role.Position);
    }

    [Fact]
    public void Role_array_parses()
    {
        Assert.Single(Parse<DiscordRole[]>("[" + Json.Role + "]"));
    }

    [Fact]
    public void Guild_populates_nested_roles_and_counts()
    {
        DiscordGuild guild = Parse<DiscordGuild>(Json.Guild);

        Assert.Equal(73193882359173120UL, guild.Owner_Id);
        Assert.Equal(12, guild.Member_Count);
        Assert.Equal(3447003, Assert.Single(guild.Roles!).Color);
        Assert.Single(guild.Emojis!);
        Assert.Equal("en-US", guild.Preferred_Locale);
    }

    [Fact]
    public void Channel_populates_parent_guild_and_overwrites()
    {
        DiscordChannel channel = Parse<DiscordChannel>(Json.Channel);

        Assert.Equal(399942396007890945UL, channel.Parent_Id);
        Assert.Equal(41771983423143938UL, channel.Guild_Id);
        ChannelPermissionOverwrite overwrite = Assert.Single(channel.Permission_Overwrites!);
        Assert.Equal(1024UL, overwrite.Allow);
        Assert.Equal(2048UL, overwrite.Deny);
    }

    [Fact]
    public void Message_author_keeps_its_fields()
    {
        DiscordMessage message = Parse<DiscordMessage>(Json.Message);

        Assert.Equal("8342729096ea3675442027381ff50dfe", message.Author.Avatar);
        Assert.Single(message.Attachments!);
    }

    [Fact]
    public void Audit_log_reads_string_encoded_numbers()
    {
        DiscordAuditLog log = Parse<DiscordAuditLog>("""
            {"audit_log_entries":[
              {"id":"1","action_type":72,"options":{"count":"5","channel_id":"2"}},
              {"id":"2","action_type":21,"options":{"delete_member_days":"7","members_removed":"3"}}
            ]}
            """);

        Assert.Equal(5, log.audit_log_entries[0].options!.count);
        Assert.Equal(7, log.audit_log_entries[1].options!.delete_member_days);
        Assert.Equal(3, log.audit_log_entries[1].options!.members_removed);
    }

    [Fact]
    public void Serializing_an_entity_does_not_follow_guild_back_references()
    {
        DiscordMember member = Parse<DiscordMember>(Json.Member);
        member.Guild = Parse<DiscordGuild>(Json.Guild);

        string json = JsonSerializer.Serialize(member, Json.Options);

        Assert.DoesNotContain("\"guild\"", json);
    }

    [Fact]
    public void Permission_overwrites_serialize_only_discord_fields()
    {
        ChannelPermissionOverwrite overwrite = new() { Id = 1, Type = 0, Allow = 2, Deny = 3 };

        string json = JsonSerializer.Serialize(overwrite, Json.Options);

        Assert.Equal("""{"id":"1","type":0,"allow":"2","deny":"3"}""", json);
    }

    [Fact]
    public void Options_ToString_hides_the_token()
    {
        Assert.DoesNotContain("super-secret", new DiscordBotOptions { Token = "super-secret" }.ToString());
    }
}
