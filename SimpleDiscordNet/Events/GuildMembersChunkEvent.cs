using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Gateway payload for GUILD_MEMBERS_CHUNK dispatch.
/// Contains a chunk of members from REQUEST_GUILD_MEMBERS.
/// </summary>
public sealed record GuildMembersChunkEvent
{
    public required ulong GuildId { get; init; }
    public required DiscordMember[] Members { get; init; }
    public int ChunkIndex { get; init; }
    public int ChunkCount { get; init; }

    /// <summary>
    /// The guild associated with this chunk, if available in cache.
    /// </summary>
    public DiscordGuild? Guild => Context.DiscordContext.GetGuild(GuildId);
}
