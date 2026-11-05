namespace SimpleDiscordNet.Sharding;

/// <summary>
/// Normalizes the URLs nodes use to reach each other.
/// </summary>
internal static class ShardUrl
{
    /// <summary>
    /// Removes trailing slashes, so appending "/register" can't produce a path that matches no route.
    /// </summary>
    public static string Normalize(string url) => url.TrimEnd('/');

    /// <summary>
    /// Turns a listen address into one other nodes can call. Wildcard hosts like http://+:8080/ are
    /// only valid for listening, so they are replaced with this machine's name.
    /// </summary>
    public static string ToAdvertised(string listenUrl)
        => ToAdvertised(listenUrl, Environment.MachineName);

    internal static string ToAdvertised(string listenUrl, string machineName)
    {
        return Normalize(listenUrl)
            .Replace("://+", $"://{machineName}", StringComparison.Ordinal)
            .Replace("://*", $"://{machineName}", StringComparison.Ordinal);
    }

    /// <summary>
    /// HttpListener only accepts prefixes that end with a slash.
    /// </summary>
    public static string ToListenerPrefix(string listenUrl) => Normalize(listenUrl) + "/";
}
