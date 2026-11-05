namespace SimpleDiscordNet.Models;

// Minimal subset used for command registration, followups and owner checks
public sealed record ApplicationInfo
{
    public required string Id { get; init; }

    /// <summary>The app's owner. Check <see cref="Team"/> first, because a team-owned app reports a placeholder here.</summary>
    public ApplicationUser? Owner { get; init; }

    /// <summary>The team that owns the app, if any.</summary>
    public ApplicationTeam? Team { get; init; }
}

public sealed record ApplicationUser
{
    public required ulong Id { get; init; }
}

public sealed record ApplicationTeam
{
    public ulong Owner_User_Id { get; init; }
    public ApplicationTeamMember[] Members { get; init; } = [];
}

public sealed record ApplicationTeamMember
{
    public required ApplicationUser User { get; init; }

    /// <summary>admin, developer or read_only. Discord doesn't list the team's owner here as a role.</summary>
    public string? Role { get; init; }
}
