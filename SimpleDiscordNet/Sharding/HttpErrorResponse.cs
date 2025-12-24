namespace SimpleDiscordNet.Sharding;

/// <summary>
/// HTTP error response for shard coordination endpoints.
/// </summary>
internal sealed class HttpErrorResponse
{
    public required string error { get; init; }
}
