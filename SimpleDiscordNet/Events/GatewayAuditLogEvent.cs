using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Internal gateway payload for audit log entry + guild id dispatches.
/// </summary>
internal sealed record GatewayAuditLogEvent
{
    public required ulong GuildId { get; init; }
    public required DiscordAuditLogEntry Entry { get; init; }
}
