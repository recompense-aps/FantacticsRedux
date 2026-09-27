using Fantactics.Core.Events;
using Fantactics.Core.Geometry;
using Fantactics.Core.State;

namespace Fantactics.Core.Engine;

/// <summary>Resolves clashes after movement: alternating strikes to the death (GameDesign §4.1, §4.3).</summary>
internal static class ClashResolver
{
    /// <summary>Resolves every clash in the order they were marked.</summary>
    /// <returns>The new state and the clash winners (who get no action this turn).</returns>
    public static (GameState State, HashSet<int> Winners) Resolve(
        GameState state,
        IEnumerable<PendingClash> clashes,
        List<GameEvent> events)
    {
        HashSet<int> winners = [];
        foreach (PendingClash clash in clashes)
        {
            Unit a = state.Units[clash.UnitA];
            Unit b = state.Units[clash.UnitB];
            (Unit first, Unit second) = StrikesFirst(state, a, b) ? (a, b) : (b, a);
            Point firstTile = first.Position;
            Point secondTile = second.Position;

            int? winner = null;
            int attacker = first.Id;
            int defender = second.Id;
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

            if (winner is int winnerId)
            {
                Point from = winnerId == first.Id ? firstTile : secondTile;
                Point to = clash.Tile ?? (winnerId == first.Id ? secondTile : firstTile);
                // The winner stays put if something ended up on the tile after all.
                if (state.UnitAt(to) is null)
                {
                    state = state.WithUnit(state.Units[winnerId] with { Position = to });
                    events.Add(new UnitStepped(0, winnerId, from, to));
                }

                winners.Add(winnerId);
            }

            events.Add(new ClashResolved(clash.UnitA, clash.UnitB, winner));
        }

        return (state, winners);
    }

    /// <summary>Higher initiative strikes first; ties go to the seat with tie priority.</summary>
    private static bool StrikesFirst(GameState state, Unit a, Unit b)
    {
        int initiativeA = state.DefinitionOf(a).Initiative;
        int initiativeB = state.DefinitionOf(b).Initiative;
        return initiativeA != initiativeB ? initiativeA > initiativeB : a.Owner == state.TiePriority;
    }
}
