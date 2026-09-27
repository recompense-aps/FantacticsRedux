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
            .SelectMany(group => Interleave(group, state.TiePriority))
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

    /// <summary>Ties alternate between players, starting with the seat that has priority; each side goes by id.</summary>
    private static IEnumerable<int> Interleave(IEnumerable<Unit> tied, Seat priority)
    {
        List<int> first = tied.Where(unit => unit.Owner == priority).Select(unit => unit.Id).Order().ToList();
        List<int> second = tied.Where(unit => unit.Owner != priority).Select(unit => unit.Id).Order().ToList();
        return Enumerable.Range(0, Math.Max(first.Count, second.Count))
            .SelectMany(i => first.Skip(i).Take(1).Concat(second.Skip(i).Take(1)));
    }
}
