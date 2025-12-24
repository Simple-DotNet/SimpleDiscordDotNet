using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Event payload for audit log entries.
/// Fired when a new audit log entry is created (requires GUILD_MODERATION intent).
/// </summary>
public sealed record AuditLogEvent
{
    /// <summary>The audit log entry that was created.</summary>
    public required DiscordAuditLogEntry Entry { get; init; }

    /// <summary>The guild where this audit log entry was created.</summary>
    public required DiscordGuild Guild { get; init; }

    /// <summary>The user who performed the action, if available in cache.</summary>
    public DiscordUser? User { get; init; }

    /// <summary>The target user, if the action was performed on a user and available in cache.</summary>
    public DiscordUser? TargetUser { get; init; }

    /// <summary>Convenience property for the action type as an enum.</summary>
    public AuditLogAction Action => (AuditLogAction)Entry.ActionType;

    /// <summary>Returns true if this action has a reason provided.</summary>
    public bool HasReason => !string.IsNullOrEmpty(Entry.Reason);
}
