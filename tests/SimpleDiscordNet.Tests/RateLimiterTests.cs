using System.Diagnostics;
using System.Globalization;
using System.Net;
using SimpleDiscordNet.Rest;
using Xunit;

namespace SimpleDiscordNet.Tests;

public class RateLimiterTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static HttpResponseMessage Response(string bucket, int remaining, double resetAfter)
    {
        HttpResponseMessage response = new(HttpStatusCode.OK);
        response.Headers.Add("X-RateLimit-Bucket", bucket);
        response.Headers.Add("X-RateLimit-Limit", "5");
        response.Headers.Add("X-RateLimit-Remaining", remaining.ToString(CultureInfo.InvariantCulture));
        response.Headers.Add("X-RateLimit-Reset-After", resetAfter.ToString(CultureInfo.InvariantCulture));
        return response;
    }

    private static async Task<long> TimeAcquireAsync(RateLimiter limiter, HttpMethod method, string route)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        using RateLimitHandle handle = await limiter.AcquireAsync(method, route, Ct);
        return stopwatch.ElapsedMilliseconds;
    }

    [Fact]
    public async Task Channels_sharing_a_bucket_hash_are_limited_independently()
    {
        using RateLimiter limiter = new();
        await limiter.UpdateFromResponseAsync(HttpMethod.Post, "/channels/1/messages", Response("hash", 0, 3));
        await limiter.UpdateFromResponseAsync(HttpMethod.Post, "/channels/2/messages", Response("hash", 4, 3));

        long waited = await TimeAcquireAsync(limiter, HttpMethod.Post, "/channels/2/messages");

        Assert.True(waited < 500, $"Channel 2 waited {waited}ms for channel 1's limit.");
    }

    [Fact]
    public async Task An_exhausted_channel_waits_for_its_own_reset()
    {
        using RateLimiter limiter = new();
        await limiter.UpdateFromResponseAsync(HttpMethod.Post, "/channels/1/messages", Response("hash", 0, 0.6));

        long waited = await TimeAcquireAsync(limiter, HttpMethod.Post, "/channels/1/messages");

        Assert.True(waited >= 400, $"Waited only {waited}ms.");
    }

    [Fact]
    public async Task Get_and_post_on_one_path_do_not_share_a_bucket()
    {
        using RateLimiter limiter = new();
        await limiter.UpdateFromResponseAsync(HttpMethod.Post, "/channels/1/messages", Response("post-hash", 0, 3));
        await limiter.UpdateFromResponseAsync(HttpMethod.Get, "/channels/1/messages", Response("get-hash", 4, 3));

        long waited = await TimeAcquireAsync(limiter, HttpMethod.Get, "/channels/1/messages");

        Assert.True(waited < 500, $"GET waited {waited}ms for the POST bucket.");
    }

    [Fact]
    public async Task A_learned_hash_applies_to_new_requests_on_the_same_channel()
    {
        using RateLimiter limiter = new();
        await limiter.UpdateFromResponseAsync(HttpMethod.Delete, "/channels/1/messages/10", Response("hash", 0, 0.6));

        long waited = await TimeAcquireAsync(limiter, HttpMethod.Delete, "/channels/1/messages/20");

        Assert.True(waited >= 400, $"A different message id should share the limit, but waited only {waited}ms.");
    }

    [Fact]
    public async Task Global_429_pauses_unrelated_routes()
    {
        using RateLimiter limiter = new();
        HttpResponseMessage limited = new((HttpStatusCode)429);
        limited.Headers.TryAddWithoutValidation("Retry-After", "0.6");
        limited.Headers.Add("X-RateLimit-Global", "true");

        await limiter.Handle429Async(HttpMethod.Post, "/channels/1/messages", limited, Ct);
        long waited = await TimeAcquireAsync(limiter, HttpMethod.Get, "/guilds/9");

        Assert.True(waited >= 400, $"Waited only {waited}ms.");
    }

    [Fact]
    public async Task Interaction_callbacks_skip_the_global_limit()
    {
        using RateLimiter limiter = new();
        HttpResponseMessage limited = new((HttpStatusCode)429);
        limited.Headers.Add("Retry-After", "3");
        limited.Headers.Add("X-RateLimit-Global", "true");
        await limiter.Handle429Async(HttpMethod.Post, "/channels/1/messages", limited, Ct);

        long waited = await TimeAcquireAsync(limiter, HttpMethod.Post, "/interactions/1/token/callback");

        Assert.True(waited < 500, $"Interaction callback waited {waited}ms.");
    }

    [Fact]
    public async Task Monitor_reports_bucket_state()
    {
        using RateLimiter limiter = new();
        await limiter.UpdateFromResponseAsync(HttpMethod.Post, "/channels/1/messages", Response("exhausted", 0, 30));
        await limiter.UpdateFromResponseAsync(HttpMethod.Get, "/guilds/2", Response("healthy", 4, 30));
        HttpResponseMessage limited = new((HttpStatusCode)429);
        limited.Headers.Add("Retry-After", "30");
        await limiter.Handle429Async(HttpMethod.Post, "/channels/1/messages", limited, Ct);

        Assert.Equal(2, limiter.GetAllBuckets().Count);
        Assert.Equal("/channels/:major/messages", Assert.Single(limiter.GetRateLimitedBuckets()).Route);
        Assert.Equal("/channels/:major/messages", limiter.GetMostUtilizedBucket()!.Route);
        Assert.Equal(1, limiter.GetTotal429Count());
    }

    [Fact]
    public async Task Fractional_reset_timestamps_are_not_truncated()
    {
        using RateLimiter limiter = new();
        double reset = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0 + 0.7;
        HttpResponseMessage response = new(HttpStatusCode.OK);
        response.Headers.Add("X-RateLimit-Bucket", "hash");
        response.Headers.Add("X-RateLimit-Remaining", "0");
        response.Headers.Add("X-RateLimit-Reset", reset.ToString("F3", CultureInfo.InvariantCulture));
        await limiter.UpdateFromResponseAsync(HttpMethod.Post, "/channels/1/messages", response);

        long waited = await TimeAcquireAsync(limiter, HttpMethod.Post, "/channels/1/messages");

        Assert.True(waited >= 400, $"Waited only {waited}ms.");
    }
}

public class RestRouteTests
{
    [Theory]
    [InlineData("/channels/123/messages", "/channels/:major/messages")]
    [InlineData("/channels/123/messages/456", "/channels/:major/messages/:id")]
    [InlineData("/channels/123/messages?limit=50&before=9", "/channels/:major/messages")]
    [InlineData("/channels/1/messages/2/reactions/%F0%9F%91%8D/@me", "/channels/:major/messages/:id/reactions/:emoji/@me")]
    [InlineData("/guilds/1/members/2/roles/3", "/guilds/:major/members/:id/roles/:id")]
    [InlineData("/webhooks/1/secret/messages/@original", "/webhooks/:major/:token/messages/@original")]
    [InlineData("/interactions/1/secret/callback", "/interactions/:id/:token/callback")]
    [InlineData("/users/@me/guilds", "/users/@me/guilds")]
    public void Template_removes_ids_and_tokens(string route, string expected)
    {
        Assert.Equal(expected, RestRoute.GetTemplate(route));
    }

    [Theory]
    [InlineData("/channels/123/messages", "123")]
    [InlineData("/guilds/456/roles", "456")]
    [InlineData("/users/@me", "")]
    public void Major_parameter_is_the_top_level_resource(string route, string expected)
    {
        Assert.Equal(expected, RestRoute.GetMajorParameter(route));
    }

    [Fact]
    public void Major_parameter_never_contains_a_token()
    {
        Assert.DoesNotContain("secret", RestRoute.GetMajorParameter("/webhooks/1/secret/messages/2"));
    }

    [Theory]
    [InlineData("/interactions/1/secret/callback", "/interactions/1/***/callback")]
    [InlineData("/webhooks/1/secret", "/webhooks/1/***")]
    [InlineData("/webhooks/1", "/webhooks/1")]
    [InlineData("/channels/1/messages", "/channels/1/messages")]
    public void Redact_hides_tokens(string route, string expected)
    {
        Assert.Equal(expected, RestRoute.Redact(route));
    }

    [Theory]
    [InlineData("/interactions/1/token/callback", true)]
    [InlineData("/webhooks/1/token", true)]
    [InlineData("/webhooks/1", false)]
    [InlineData("/channels/1/messages", false)]
    public void Only_token_routes_skip_the_global_limit(string route, bool exempt)
    {
        Assert.Equal(exempt, RestRoute.IsGlobalLimitExempt(route));
    }
}
