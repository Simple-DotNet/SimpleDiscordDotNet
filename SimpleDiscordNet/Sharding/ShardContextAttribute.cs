namespace SimpleDiscordNet.Sharding;

/// <summary>
/// Attribute to inject shard information into command handler parameters.
/// The source generator automatically provides the shard handling the interaction.
/// Example:
/// <code>
/// [SlashCommand("shard", "Get shard info")]
/// public async Task ShardAsync(InteractionContext ctx, [ShardContext] ShardInfo shard)
/// {
///     await ctx.RespondAsync($"Shard {shard.Id}: {shard.GuildCount} guilds, {shard.Latency}ms latency");
/// }
/// </code>
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class ShardContextAttribute : Attribute
{
}
