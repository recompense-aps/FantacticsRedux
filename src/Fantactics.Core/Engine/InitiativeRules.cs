using System.Collections.Immutable;
using Fantactics.Core.Events;
using Fantactics.Core.State;

namespace Fantactics.Core.Engine;

/// <summary>Builds the action order after movement (GameDesign §4.1).</summary>
internal static class InitiativeRules
{
    /// <summary>Computes effective initiative, orders every unit that can act, and opens the action phase.</summary>
    public static GameState BeginActionPhase(
        GameState state,
        ImmutableSortedSet<int> held,
        ImmutableSortedSet<int> braced,
        ImmutableSortedSet<int> clashWinners,
        List<GameEvent> events)
    {
        ImmutableSortedDictionary<int, int> effective = state.FieldUnits.ToImmutableSortedDictionary(
            unit => unit.Id,
            unit => state.DefinitionOf(unit).Initiative
                + (braced.Contains(unit.Id) ? state.Rules.BracedBonus
                    : held.Contains(unit.Id) ? state.Rules.HeldBonus
                    : 0));

        ImmutableArray<int> order = state.FieldUnits
            .Where(unit => !clashWinners.Contains(unit.Id) && unit.CannotActOnTurn != state.Turn)
            .GroupBy(unit => effective[unit.Id])
            .OrderByDescending(group => group.Key)
            .SelectMany(group => Interleave(group, state))
            .ToImmutableArray();

        state = state with
        {
            Phase = Phase.Action,
            TurnState = TurnState.Empty with
            {
                Held = held,
                Braced = braced,
                ClashWinners = clashWinners,
                EffectiveInitiative = effective,
                ActionQueue = order,
            },
        };
        events.Add(new InitiativeOrdered(order));
        return order.IsEmpty ? TurnRules.EndTurn(state, events) : state;
    }

    /// <summary>
    /// Ties take turns between players in tie order, starting with the seat that has priority (GameDesign §4.1); each
    /// player goes by id.
    /// </summary>
    private static IEnumerable<int> Interleave(IEnumerable<Unit> tied, GameState state)
    {
        List<List<int>> bySeat = state.TieOrder
            .Select(seat => tied
                .Where(unit => unit.Owner == seat)
                .Select(unit => unit.Id)
                .Order()
                .ToList())
            .ToList();
        return Enumerable.Range(0, bySeat.Max(ids => ids.Count))
            .SelectMany(i => bySeat.SelectMany(ids => ids.Skip(i).Take(1)));
    }
}
