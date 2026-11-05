namespace SimpleDiscordNet.Core;

/// <summary>
/// Details about this library that it reports to Discord.
/// </summary>
internal static class LibraryInfo
{
    private const string ProjectUrl = "https://github.com/Simple-DotNet/SimpleDiscordDotNet";

    /// <summary>
    /// The User-Agent Discord asks clients to send: DiscordBot ($url, $version).
    /// </summary>
    public static readonly string UserAgent =
        $"DiscordBot ({ProjectUrl}, {typeof(LibraryInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"})";
}
