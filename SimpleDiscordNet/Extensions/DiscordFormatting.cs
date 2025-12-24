namespace SimpleDiscordNet.Extensions;

/// <summary>
/// Helper methods for Discord markdown formatting.
/// Makes it easy for beginners to format text properly.
/// </summary>
public static class DiscordFormatting
{
    /// <summary>
    /// Makes text bold in Discord.
    /// Example: string bold = DiscordFormatting.Bold("Important");
    /// </summary>
    public static string Bold(string text) => Bold(text.AsSpan());

    /// <summary>
    /// Makes text bold in Discord (span overload for zero-allocation formatting).
    /// </summary>
    public static string Bold(ReadOnlySpan<char> text)
    {
        int length = text.Length + 4;
        return length <= 256
            ? string.Create(length, text, static (span, txt) =>
            {
                span[0] = span[1] = '*';
                txt.CopyTo(span[2..]);
                span[^2] = span[^1] = '*';
            })
            : $"**{text}**";
    }

    /// <summary>
    /// Makes text italic in Discord.
    /// Example: string italic = DiscordFormatting.Italic("Emphasis");
    /// </summary>
    public static string Italic(string text) => Italic(text.AsSpan());

    /// <summary>
    /// Makes text italic in Discord (span overload for zero-allocation formatting).
    /// </summary>
    public static string Italic(ReadOnlySpan<char> text)
    {
        int length = text.Length + 2;
        return length <= 256
            ? string.Create(length, text, static (span, txt) =>
            {
                span[0] = '*';
                txt.CopyTo(span[1..]);
                span[^1] = '*';
            })
            : $"*{text}*";
    }

    /// <summary>
    /// Makes text underlined in Discord.
    /// Example: string underline = DiscordFormatting.Underline("Notice");
    /// </summary>
    public static string Underline(string text) => Underline(text.AsSpan());

    /// <summary>
    /// Makes text underlined in Discord (span overload for zero-allocation formatting).
    /// </summary>
    public static string Underline(ReadOnlySpan<char> text)
    {
        int length = text.Length + 4;
        return length <= 256
            ? string.Create(length, text, static (span, txt) =>
            {
                span[0] = span[1] = '_';
                txt.CopyTo(span[2..]);
                span[^2] = span[^1] = '_';
            })
            : $"__{text}__";
    }

    /// <summary>
    /// Makes text strikethrough in Discord.
    /// Example: string strike = DiscordFormatting.Strikethrough("Old");
    /// </summary>
    public static string Strikethrough(string text) => Strikethrough(text.AsSpan());

    /// <summary>
    /// Makes text strikethrough in Discord (span overload for zero-allocation formatting).
    /// </summary>
    public static string Strikethrough(ReadOnlySpan<char> text)
    {
        int length = text.Length + 4;
        return length <= 256
            ? string.Create(length, text, static (span, txt) =>
            {
                span[0] = span[1] = '~';
                txt.CopyTo(span[2..]);
                span[^2] = span[^1] = '~';
            })
            : $"~~{text}~~";
    }

    /// <summary>
    /// Makes text appear as inline code in Discord.
    /// Example: string code = DiscordFormatting.Code("variable");
    /// </summary>
    public static string Code(string text) => Code(text.AsSpan());

    /// <summary>
    /// Makes text appear as inline code in Discord (span overload for zero-allocation formatting).
    /// </summary>
    public static string Code(ReadOnlySpan<char> text)
    {
        int length = text.Length + 2;
        return length <= 256
            ? string.Create(length, text, static (span, txt) =>
            {
                span[0] = '`';
                txt.CopyTo(span[1..]);
                span[^1] = '`';
            })
            : $"`{text}`";
    }

    /// <summary>
    /// Makes text appear as a code block in Discord with optional language syntax highlighting.
    /// Example: string block = DiscordFormatting.CodeBlock("console.log('Hello');", "javascript");
    /// </summary>
    public static string CodeBlock(string text, string? language = null)
        => language is null ? $"```\n{text}\n```" : $"```{language}\n{text}\n```";

    /// <summary>
    /// Creates a Discord quote/blockquote.
    /// Example: string quote = DiscordFormatting.Quote("Wise words");
    /// </summary>
    public static string Quote(string text) => Quote(text.AsSpan());

    /// <summary>
    /// Creates a Discord quote/blockquote (span overload for zero-allocation formatting).
    /// </summary>
    public static string Quote(ReadOnlySpan<char> text)
    {
        int length = text.Length + 2;
        return length <= 256
            ? string.Create(length, text, static (span, txt) =>
            {
                span[0] = '>';
                span[1] = ' ';
                txt.CopyTo(span[2..]);
            })
            : $"> {text}";
    }

    /// <summary>
    /// Creates a spoiler (hidden until clicked).
    /// Example: string spoiler = DiscordFormatting.Spoiler("Secret");
    /// </summary>
    public static string Spoiler(string text) => Spoiler(text.AsSpan());

    /// <summary>
    /// Creates a spoiler (hidden until clicked) (span overload for zero-allocation formatting).
    /// </summary>
    public static string Spoiler(ReadOnlySpan<char> text)
    {
        int length = text.Length + 4;
        return length <= 256
            ? string.Create(length, text, static (span, txt) =>
            {
                span[0] = span[1] = '|';
                txt.CopyTo(span[2..]);
                span[^2] = span[^1] = '|';
            })
            : $"||{text}||";
    }

    /// <summary>
    /// Mentions a user by ID.
    /// Example: string mention = DiscordFormatting.MentionUser("123456789");
    /// </summary>
    public static string MentionUser(string userId) => MentionUser(userId.AsSpan());

    /// <summary>
    /// Mentions a user by ID (span overload for zero-allocation formatting).
    /// </summary>
    public static string MentionUser(ReadOnlySpan<char> userId)
    {
        int length = userId.Length + 3;
        return length <= 256
            ? string.Create(length, userId, static (span, id) =>
            {
                span[0] = '<';
                span[1] = '@';
                id.CopyTo(span[2..]);
                span[^1] = '>';
            })
            : $"<@{userId}>";
    }

    /// <summary>
    /// Mentions a channel by ID.
    /// Example: string mention = DiscordFormatting.MentionChannel("123456789");
    /// </summary>
    public static string MentionChannel(string channelId) => MentionChannel(channelId.AsSpan());

    /// <summary>
    /// Mentions a channel by ID (span overload for zero-allocation formatting).
    /// </summary>
    public static string MentionChannel(ReadOnlySpan<char> channelId)
    {
        int length = channelId.Length + 3;
        return length <= 256
            ? string.Create(length, channelId, static (span, id) =>
            {
                span[0] = '<';
                span[1] = '#';
                id.CopyTo(span[2..]);
                span[^1] = '>';
            })
            : $"<#{channelId}>";
    }

    /// <summary>
    /// Mentions a role by ID.
    /// Example: string mention = DiscordFormatting.MentionRole("123456789");
    /// </summary>
    public static string MentionRole(string roleId) => MentionRole(roleId.AsSpan());

    /// <summary>
    /// Mentions a role by ID (span overload for zero-allocation formatting).
    /// </summary>
    public static string MentionRole(ReadOnlySpan<char> roleId)
    {
        int length = roleId.Length + 4;
        return length <= 256
            ? string.Create(length, roleId, static (span, id) =>
            {
                span[0] = '<';
                span[1] = '@';
                span[2] = '&';
                id.CopyTo(span[3..]);
                span[^1] = '>';
            })
            : $"<@&{roleId}>";
    }

    /// <summary>
    /// Creates a clickable link.
    /// Example: string link = DiscordFormatting.Link("Click here", "https://example.com");
    /// </summary>
    public static string Link(string text, string url) => $"[{text}]({url})";

    /// <summary>
    /// Formats a Discord timestamp that shows in user's local time.
    /// Example: string time = DiscordFormatting.Timestamp(DateTimeOffset.Now);
    /// </summary>
    public static string Timestamp(DateTimeOffset time, TimestampStyle style = TimestampStyle.ShortDateTime)
        => Timestamp(time.ToUnixTimeSeconds(), style);

    /// <summary>
    /// Formats a Discord timestamp that shows in user's local time (span-optimized overload).
    /// </summary>
    public static string Timestamp(long unixSeconds, TimestampStyle style = TimestampStyle.ShortDateTime)
    {
        return string.Create(32, (unixSeconds, style), static (span, state) =>
        {
            span[0] = '<';
            span[1] = 't';
            span[2] = ':';
            state.unixSeconds.TryFormat(span[3..], out int written);
            int pos = 3 + written;
            span[pos++] = ':';
            span[pos++] = (char)state.style;
            span[pos] = '>';
            span = span[..(pos + 1)];
        });
    }

    /// <summary>
    /// Creates a bulleted list.
    /// Example: string list = DiscordFormatting.BulletList("Item 1", "Item 2", "Item 3");
    /// </summary>
    public static string BulletList(params string[] items)
    {
        if (items.Length == 0) return string.Empty;
        if (items.Length == 1) return $"• {items[0]}";

        int totalLength = 0;
        foreach (var item in items)
            totalLength += item.Length + 3; // "• " + item + "\n"
        totalLength -= 1; // no trailing newline

        return string.Create(totalLength, items, static (span, itms) =>
        {
            int pos = 0;
            for (int i = 0; i < itms.Length; i++)
            {
                if (i > 0) span[pos++] = '\n';
                span[pos++] = '•';
                span[pos++] = ' ';
                itms[i].AsSpan().CopyTo(span[pos..]);
                pos += itms[i].Length;
            }
        });
    }

    /// <summary>
    /// Creates a numbered list.
    /// Example: a string list = DiscordFormatting.NumberedList("First", "Second", "Third");
    /// </summary>
    public static string NumberedList(params string[] items)
    {
        if (items.Length == 0) return string.Empty;
        if (items.Length == 1) return $"1. {items[0]}";

        int totalLength = 0;
        for (int i = 0; i < items.Length; i++)
        {
            int numDigits = (i + 1).ToString().Length;
            totalLength += numDigits + 2 + items[i].Length + 1; // number + ". " + item + "\n"
        }
        totalLength -= 1; // no trailing newline

        return string.Create(totalLength, items, static (span, itms) =>
        {
            int pos = 0;
            for (int i = 0; i < itms.Length; i++)
            {
                if (i > 0) span[pos++] = '\n';
                (i + 1).TryFormat(span[pos..], out int written);
                pos += written;
                span[pos++] = '.';
                span[pos++] = ' ';
                itms[i].AsSpan().CopyTo(span[pos..]);
                pos += itms[i].Length;
            }
        });
    }
}

/// <summary>
/// Discord timestamp formatting styles.
/// </summary>
public enum TimestampStyle
{
    /// <summary>16:20</summary>
    ShortTime = 't',
    /// <summary>16:20:30</summary>
    LongTime = 'T',
    /// <summary>20/04/2021</summary>
    ShortDate = 'd',
    /// <summary>20 April 2021</summary>
    LongDate = 'D',
    /// <summary>20 April 2021 16:20</summary>
    ShortDateTime = 'f',
    /// <summary>Tuesday, 20 April 2021 16:20</summary>
    LongDateTime = 'F',
    /// <summary>2 months ago</summary>
    Relative = 'R'
}
