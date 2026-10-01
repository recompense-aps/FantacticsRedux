using Fantactics.Core.Events;
using Fantactics.Core.Geometry;
using Fantactics.Core.State;

namespace Fantactics.Core.Engine;

/// <summary>Resolves clashes after movement: alternating strikes to the death (GameDesign §4.1, §4.3).</summary>
internal static class ClashResolver
{
    /// <summary>
    /// Resolves every clash in the order they were marked. In a clash of three or more teams, the survivor of each
    /// fight takes on the next challenger; a fight that ends with both alive stops the chain there.
    /// </summary>
    /// <returns>The new state and the clash winners (who get no action this turn).</returns>
    public static (GameState State, HashSet<int> Winners) Resolve(
        GameState state,
        IEnumerable<PendingClash> clashes,
        List<GameEvent> events)
    {
        HashSet<int> winners = [];
        foreach (PendingClash clash in clashes)
        {
            Dictionary<int, Point> starts = new[] { clash.UnitA, clash.UnitB }
                .Concat(clash.Challengers)
                .ToDictionary(id => id, id => state.Units[id].Position);
            (state, int? winner) = Fight(state, clash.UnitA, clash.UnitB, events);
            foreach (int challenger in clash.Challengers)
            {
                if (winner is not int survivor)
                {
                    break;
                }

                (state, winner) = Fight(state, survivor, challenger, events);
            }

            if (winner is int winnerId)
            {
                Point from = starts[winnerId];
                Point to = clash.Tile ?? starts[winnerId == clash.UnitA ? clash.UnitB : clash.UnitA];
                // The winner stays put if something ended up on the tile after all.
                if (state.UnitAt(to) is null)
                {
                    state = state.WithUnit(state.Units[winnerId] with { Position = to });
                    events.Add(new UnitStepped(0, winnerId, from, to));
                }

                winners.Add(winnerId);
            }
        }

        return (state, winners);
    }

    /// <summary>
    /// Two units trade clash strikes, higher initiative first, until one dies. Clashes are pure fighting: only
    /// damage modifiers and clash-specific traits apply, never on-hit effects (see <see cref="CombatRules.Strike"/>).
    /// </summary>
    /// <returns>The new state and the survivor, or <c>null</c> if the strike cap was reached with both alive.</returns>
    public static (GameState State, int? Winner) Fight(GameState state, int unitA, int unitB, List<GameEvent> events)
    {
        (int attacker, int defender) = StrikesFirst(state, state.Units[unitA], state.Units[unitB])
            ? (unitA, unitB)
            : (unitB, unitA);
        int? winner = null;
        for (int strike = 0; strike < state.Rules.MaxClashStrikes; strike++)
        {
            state = CombatRules.Strike(state, attacker, defender, AttackKind.Clash, events);
            if (!state.Units.ContainsKey(defender))
            {
                winner = attacker;
                break;
            }

            (attacker, defender) = (defender, attacker);
        }

        events.Add(new ClashResolved(unitA, unitB, winner));
        return (state, winner);
    }

    /// <summary>Higher initiative strikes first; ties go to the seat earlier in this turn's tie order.</summary>
    private static bool StrikesFirst(GameState state, Unit a, Unit b)
    {
        int initiativeA = state.DefinitionOf(a).Initiative;
        int initiativeB = state.DefinitionOf(b).Initiative;
        if (initiativeA != initiativeB)
        {
            return initiativeA > initiativeB;
        }

        List<Seat> order = [.. state.TieOrder];
        return order.IndexOf(a.Owner) < order.IndexOf(b.Owner);
    }
}
