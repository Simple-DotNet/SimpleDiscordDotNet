using SimpleDiscordNet.Models;
using SimpleDiscordNet.Models.Requests;

namespace SimpleDiscordNet.Primitives;

/// <summary>
/// Strongly-typed message payload for Discord API requests.
/// Used by MessageBuilder to create AoT-compatible message objects without reflection.
/// </summary>
public sealed class MessagePayload
{
    public string? content { get; set; }
    public Embed[]? embeds { get; set; }
    public object[]? components { get; set; }
    public object? allowed_mentions { get; set; }
    public object[]? attachments { get; set; }
    public MessageReference? message_reference { get; set; }
    public string? thread_name { get; set; }
    public string[]? applied_tags { get; set; }
}
