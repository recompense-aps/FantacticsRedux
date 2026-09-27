using System.Collections.Immutable;

namespace Fantactics.Core.State;

/// <summary>Bookkeeping that only lasts for the current turn.</summary>
/// <param name="Held">Units that held this turn (GameDesign §4.1).</param>
/// <param name="Braced">Held units that also earned the Braced bonus.</param>
/// <param name="ClashWinners">Units that won a clash; they get no action (GameDesign §4.2).</param>
/// <param name="EffectiveInitiative">Initiative including bonuses, for units in the action order.</param>
/// <param name="ActionQueue">Units still to act, in order.</param>
/// <param name="DelayedQueue">Units that delayed, in their original order; they act after the queue.</param>
/// <param name="HasDelayed">Units that already used their once-per-turn Delay.</param>
/// <param name="RetaliateUsed">Units that already retaliated this turn.</param>
public sealed record TurnState(
    ImmutableSortedSet<int> Held,
    ImmutableSortedSet<int> Braced,
    ImmutableSortedSet<int> ClashWinners,
    ImmutableSortedDictionary<int, int> EffectiveInitiative,
    ImmutableArray<int> ActionQueue,
    ImmutableArray<int> DelayedQueue,
    ImmutableSortedSet<int> HasDelayed,
    ImmutableSortedSet<int> RetaliateUsed)
{
    /// <summary>A turn with nothing recorded yet.</summary>
    public static TurnState Empty { get; } = new([], [], [], ImmutableSortedDictionary<int, int>.Empty, [], [], [], []);

    /// <summary>The unit whose action slot is up, if any.</summary>
    public int? CurrentActor =>
        !ActionQueue.IsEmpty ? ActionQueue[0]
        : !DelayedQueue.IsEmpty ? DelayedQueue[0]
        : null;

    /// <summary>Removes <paramref name="unitId"/> from the action order (it acted or died).</summary>
    public TurnState WithoutActor(int unitId) =>
        this with { ActionQueue = ActionQueue.Remove(unitId), DelayedQueue = DelayedQueue.Remove(unitId) };
}
