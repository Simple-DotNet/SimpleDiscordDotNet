using SimpleDiscordNet.Commands;
using SimpleDiscordNet.Models;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Event payload for direct (DM) messages. Provides the raw message and a convenient context
/// that allows responding via REST.
/// </summary>
public sealed class DirectMessageEvent
{
    public required MessageCreateEventRaw Message { get; init; }
    public required CommandContext Context { get; init; }
}
