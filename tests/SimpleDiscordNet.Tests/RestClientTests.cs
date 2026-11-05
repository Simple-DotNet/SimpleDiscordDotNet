using System.Net;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Models.Requests;
using SimpleDiscordNet.Primitives;
using Xunit;

namespace SimpleDiscordNet.Tests;

public class RestClientTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Disconnecting_from_voice_sends_an_explicit_null()
    {
        using RestFixture fixture = new();

        await fixture.Rest.PatchGuildMemberAsync<DiscordMember>("1", "2", new DisconnectVoiceRequest(), Ct).ContinueWith(_ => { });

        Assert.Equal("""{"channel_id":null}""", fixture.Handler.LastBody);
    }

    [Fact]
    public async Task Clearing_a_timeout_sends_an_explicit_null()
    {
        using RestFixture fixture = new();

        await fixture.Rest.PatchAsync("/x", new ClearTimeoutRequest(), Ct);

        Assert.Equal("""{"communication_disabled_until":null}""", fixture.Handler.LastBody);
    }

    [Fact]
    public async Task Editing_only_an_embed_leaves_message_text_out_of_the_body()
    {
        using RestFixture fixture = new();

        await fixture.Rest.PatchAsync("/x", new EditMessageRequest { embeds = [new EmbedBuilder().WithTitle("t").Build()] }, Ct);

        Assert.DoesNotContain("content", fixture.Handler.LastBody);
    }

    [Fact]
    public async Task Permission_overwrites_are_serialized_when_creating_a_channel()
    {
        using RestFixture fixture = new();
        CreateGuildChannelRequest request = new()
        {
            name = "n",
            permission_overwrites = [new ChannelPermissionOverwrite { Id = 1, Type = 0, Allow = 2, Deny = 3 }]
        };

        await fixture.Rest.PostAsync("/x", request, Ct);

        Assert.Contains("""{"id":"1","type":0,"allow":"2","deny":"3"}""", fixture.Handler.LastBody);
    }

    [Fact]
    public async Task Requests_use_the_format_discord_expects_for_user_agent()
    {
        using RestFixture fixture = new();

        await fixture.Rest.PostAsync("/x", new CreateGuildRoleRequest { name = "r" }, Ct);

        string userAgent = Assert.Single(fixture.Handler.Requests).Headers.UserAgent.ToString();
        Assert.StartsWith("DiscordBot (https://", userAgent);
        Assert.EndsWith("1.11.0)", userAgent);
    }

    [Fact]
    public async Task Audit_reason_is_sent_url_encoded()
    {
        using RestFixture fixture = new();
        fixture.Handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.NoContent);

        await fixture.Rest.KickMemberAsync("1", "2", Ct, "bad bot");

        HttpRequestMessage request = Assert.Single(fixture.Handler.Requests);
        Assert.Equal("bad%20bot", Assert.Single(request.Headers.GetValues("X-Audit-Log-Reason")));
    }

    [Fact]
    public async Task No_audit_header_without_a_reason()
    {
        using RestFixture fixture = new();
        fixture.Handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.NoContent);

        await fixture.Rest.KickMemberAsync("1", "2", Ct);

        Assert.False(Assert.Single(fixture.Handler.Requests).Headers.Contains("X-Audit-Log-Reason"));
    }

    [Fact]
    public async Task Post_is_not_retried_after_a_server_error()
    {
        using RestFixture fixture = new();
        fixture.Handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("{}") };

        await Assert.ThrowsAsync<HttpRequestException>(() => fixture.Rest.PostAsync("/channels/1/messages", new CreateGuildRoleRequest(), Ct));

        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task Rate_limited_request_is_retried()
    {
        using RestFixture fixture = new();
        int calls = 0;
        fixture.Handler.Respond = _ =>
        {
            if (Interlocked.Increment(ref calls) > 1)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };

            HttpResponseMessage limited = new((HttpStatusCode)429);
            limited.Headers.TryAddWithoutValidation("Retry-After", "0.05");
            return limited;
        };

        await fixture.Rest.PostAsync("/channels/1/messages", new CreateGuildRoleRequest(), Ct);

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Interaction_tokens_are_never_logged()
    {
        using RestFixture fixture = new();
        fixture.Handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("nope") };

        await Assert.ThrowsAsync<HttpRequestException>(() => fixture.Rest.PostInteractionCallbackAsync("1", "secret-token", new CreateGuildRoleRequest(), Ct));
        await Assert.ThrowsAsync<HttpRequestException>(() => fixture.Rest.PostWebhookFollowupAsync("1", "secret-token", new CreateGuildRoleRequest(), Ct));
        await Assert.ThrowsAsync<HttpRequestException>(() => fixture.Rest.PatchWebhookMessageAsync<DiscordMessage>("1", "secret-token", "2", new CreateGuildRoleRequest(), Ct));

        Assert.NotEmpty(fixture.Logs);
        Assert.DoesNotContain(fixture.Logs, log => log.Contains("secret-token"));
    }
}
