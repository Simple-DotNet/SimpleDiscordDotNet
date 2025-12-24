namespace SimpleDiscordNet.Events;

/// <summary>
/// Represents a before/after snapshot of an entity change.
/// Used for update events to provide both the previous and current state.
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
public sealed record EntityChange<T> where T : class
{
    /// <summary>The state of the entity before the change. May be null if not cached.</summary>
    public T? Before { get; init; }

    /// <summary>The state of the entity after the change.</summary>
    public required T After { get; init; }

    /// <summary>Returns true if we have the before state available.</summary>
    public bool HasBefore => Before is not null;
}
