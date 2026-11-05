using System.Text;

namespace SimpleDiscordNet.Rest;

/// <summary>
/// Breaks a REST route into the parts Discord's rate limits are based on.
/// Discord tracks a limit per bucket hash and top-level resource, so one exhausted channel
/// must not hold back another channel that shares the same hash.
/// </summary>
internal static class RestRoute
{
    private const string Redacted = "***";

    private static bool IsTopLevelResource(string segment) => segment is "channels" or "guilds" or "webhooks";

    private static bool HasTokenSegment(string[] segments)
        => segments.Length > 3 && segments[1] is "interactions" or "webhooks";

    private static bool IsDigits(string segment)
    {
        if (segment.Length == 0) return false;
        foreach (char c in segment)
        {
            if (!char.IsAsciiDigit(c)) return false;
        }
        return true;
    }

    private static string[] Split(string route)
    {
        int query = route.IndexOf('?');
        return (query >= 0 ? route[..query] : route).Split('/');
    }

    /// <summary>
    /// Returns the route shared by every resource of the same kind, such as /channels/:major/messages.
    /// Contains no IDs or tokens, so it is safe to log and to use as a dictionary key.
    /// </summary>
    public static string GetTemplate(string route)
    {
        string[] segments = Split(route);
        bool hasToken = HasTokenSegment(segments);
        StringBuilder template = new(route.Length);

        for (int i = 1; i < segments.Length; i++)
        {
            string segment = segments[i];
            template.Append('/');

            if (i == 2 && IsTopLevelResource(segments[1])) template.Append(":major");
            else if (i == 3 && hasToken) template.Append(":token");
            else if (segments[i - 1] == "reactions") template.Append(":emoji");
            else if (IsDigits(segment)) template.Append(":id");
            else template.Append(segment);
        }

        return template.ToString();
    }

    /// <summary>
    /// Returns the top-level resource a limit is scoped to: a channel, guild, or webhook.
    /// A webhook's token is hashed so it can't reach logs or rate limit events.
    /// </summary>
    public static string GetMajorParameter(string route)
    {
        string[] segments = Split(route);
        if (segments.Length < 3) return string.Empty;

        if (HasTokenSegment(segments))
        {
            return $"{segments[2]}/{segments[3].GetHashCode():x}";
        }

        return IsTopLevelResource(segments[1]) ? segments[2] : string.Empty;
    }

    /// <summary>
    /// Interaction endpoints, including followups sent with an interaction token, aren't bound to the global limit.
    /// </summary>
    public static bool IsGlobalLimitExempt(string route)
    {
        string[] segments = Split(route);
        return segments.Length > 1 && (segments[1] == "interactions" || HasTokenSegment(segments));
    }

    /// <summary>
    /// Removes interaction and webhook tokens from a route so it can be logged.
    /// </summary>
    public static string Redact(string route)
    {
        string[] segments = route.Split('/');
        if (!HasTokenSegment(segments)) return route;

        segments[3] = Redacted;
        return string.Join('/', segments);
    }
}
