namespace SimpleDiscordNet.Models.Requests;

/// <summary>
/// Request payload for modifying a guild member.
/// </summary>
public sealed class ModifyGuildMemberRequest
{
    /// <summary>
    /// The member's nickname.
    /// </summary>
    public string? nick { get; set; }

    /// <summary>
    /// Array of role IDs the member is assigned.
    /// </summary>
    public ulong[]? roles { get; set; }

    /// <summary>
    /// Whether the member is muted in voice channels.
    /// </summary>
    public bool? mute { get; set; }

    /// <summary>
    /// Whether the member is deafened in voice channels.
    /// </summary>
    public bool? deaf { get; set; }

    /// <summary>
    /// ID of channel to move member to (if they are connected to voice).
    /// </summary>
    public ulong? channel_id { get; set; }

    /// <summary>
    /// When the member's timeout will expire (ISO8601 timestamp). Set to null to remove timeout.
    /// </summary>
    public string? communication_disabled_until { get; set; }

    /// <summary>
    /// Member flags (bit set).
    /// </summary>
    public int? flags { get; set; }
}