namespace SimpleDiscordNet.Entities;

/// <summary>
/// Represents a Discord audit log entry.
/// Audit logs track administrative actions in a guild.
/// </summary>
public sealed record DiscordAuditLogEntry
{
    /// <summary>ID of the affected entity (channel, user, role, etc.)</summary>
    public ulong? TargetId { get; init; }

    /// <summary>Changes made to the target</summary>
    public AuditLogChange[]? Changes { get; init; }

    /// <summary>User who made the changes</summary>
    public ulong? UserId { get; init; }

    /// <summary>ID of the entry</summary>
    public required ulong Id { get; init; }

    /// <summary>Type of action that occurred</summary>
    public required int ActionType { get; init; }

    /// <summary>Additional info for certain action types</summary>
    public AuditLogOptions? Options { get; init; }

    /// <summary>Reason for the change (0-512 characters)</summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Represents a change to an entity in an audit log entry.
/// </summary>
public sealed record AuditLogChange
{
    /// <summary>New value of the key</summary>
    public object? NewValue { get; init; }

    /// <summary>Old value of the key</summary>
    public object? OldValue { get; init; }

    /// <summary>Name of audit log change key</summary>
    public required string Key { get; init; }
}

/// <summary>
/// Optional audit log data for certain action types.
/// </summary>
public sealed record AuditLogOptions
{
    /// <summary>Number of days after which inactive members were kicked</summary>
    public int? DeleteMemberDays { get; init; }

    /// <summary>Number of members removed by the prune</summary>
    public int? MembersRemoved { get; init; }

    /// <summary>Channel in which the entities were targeted</summary>
    public ulong? ChannelId { get; init; }

    /// <summary>ID of the message that was targeted</summary>
    public ulong? MessageId { get; init; }

    /// <summary>Number of entities that were targeted</summary>
    public int? Count { get; init; }

    /// <summary>ID of the overwritten entity</summary>
    public ulong? Id { get; init; }

    /// <summary>Type of overwritten entity (role or member)</summary>
    public string? Type { get; init; }

    /// <summary>Name of the role</summary>
    public string? RoleName { get; init; }
}

/// <summary>
/// Discord audit log action types.
/// </summary>
public enum AuditLogAction
{
    /// <summary>Server settings were updated</summary>
    GuildUpdate = 1,

    /// <summary>Channel was created</summary>
    ChannelCreate = 10,
    /// <summary>Channel settings were updated</summary>
    ChannelUpdate = 11,
    /// <summary>Channel was deleted</summary>
    ChannelDelete = 12,
    /// <summary>Permission overwrite was created</summary>
    ChannelOverwriteCreate = 13,
    /// <summary>Permission overwrite was updated</summary>
    ChannelOverwriteUpdate = 14,
    /// <summary>Permission overwrite was deleted</summary>
    ChannelOverwriteDelete = 15,

    /// <summary>Member was removed from server</summary>
    MemberKick = 20,
    /// <summary>Members were pruned from server</summary>
    MemberPrune = 21,
    /// <summary>Member was banned from server</summary>
    MemberBanAdd = 22,
    /// <summary>Member was unbanned from server</summary>
    MemberBanRemove = 23,
    /// <summary>Member was updated</summary>
    MemberUpdate = 24,
    /// <summary>Member's roles were updated</summary>
    MemberRoleUpdate = 25,
    /// <summary>Member was moved to a different voice channel</summary>
    MemberMove = 26,
    /// <summary>Member was disconnected from voice</summary>
    MemberDisconnect = 27,
    /// <summary>Bot was added to server</summary>
    BotAdd = 28,

    /// <summary>Role was created</summary>
    RoleCreate = 30,
    /// <summary>Role was updated</summary>
    RoleUpdate = 31,
    /// <summary>Role was deleted</summary>
    RoleDelete = 32,

    /// <summary>Invite was created</summary>
    InviteCreate = 40,
    /// <summary>Invite was updated</summary>
    InviteUpdate = 41,
    /// <summary>Invite was deleted</summary>
    InviteDelete = 42,

    /// <summary>Webhook was created</summary>
    WebhookCreate = 50,
    /// <summary>Webhook was updated</summary>
    WebhookUpdate = 51,
    /// <summary>Webhook was deleted</summary>
    WebhookDelete = 52,

    /// <summary>Emoji was created</summary>
    EmojiCreate = 60,
    /// <summary>Emoji was updated</summary>
    EmojiUpdate = 61,
    /// <summary>Emoji was deleted</summary>
    EmojiDelete = 62,

    /// <summary>Single message was deleted</summary>
    MessageDelete = 72,
    /// <summary>Multiple messages were deleted</summary>
    MessageBulkDelete = 73,
    /// <summary>Message was pinned</summary>
    MessagePin = 74,
    /// <summary>Message was unpinned</summary>
    MessageUnpin = 75,

    /// <summary>Integration was created</summary>
    IntegrationCreate = 80,
    /// <summary>Integration was updated</summary>
    IntegrationUpdate = 81,
    /// <summary>Integration was deleted</summary>
    IntegrationDelete = 82,

    /// <summary>Stage instance was created</summary>
    StageInstanceCreate = 83,
    /// <summary>Stage instance was updated</summary>
    StageInstanceUpdate = 84,
    /// <summary>Stage instance was deleted</summary>
    StageInstanceDelete = 85,

    /// <summary>Sticker was created</summary>
    StickerCreate = 90,
    /// <summary>Sticker was updated</summary>
    StickerUpdate = 91,
    /// <summary>Sticker was deleted</summary>
    StickerDelete = 92,

    /// <summary>Scheduled event was created</summary>
    GuildScheduledEventCreate = 100,
    /// <summary>Scheduled event was updated</summary>
    GuildScheduledEventUpdate = 101,
    /// <summary>Scheduled event was deleted</summary>
    GuildScheduledEventDelete = 102,

    /// <summary>Thread was created</summary>
    ThreadCreate = 110,
    /// <summary>Thread was updated</summary>
    ThreadUpdate = 111,
    /// <summary>Thread was deleted</summary>
    ThreadDelete = 112,

    /// <summary>Auto moderation rule was created</summary>
    AutoModerationRuleCreate = 140,
    /// <summary>Auto moderation rule was updated</summary>
    AutoModerationRuleUpdate = 141,
    /// <summary>Auto moderation rule was deleted</summary>
    AutoModerationRuleDelete = 142,
    /// <summary>Auto moderation blocked a message</summary>
    AutoModerationBlockMessage = 143,
    /// <summary>Auto moderation flagged a message</summary>
    AutoModerationFlagToChannel = 144,
    /// <summary>Auto moderation timed out a member</summary>
    AutoModerationUserCommunicationDisabled = 145,
}
