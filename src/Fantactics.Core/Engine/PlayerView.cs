using System.Collections.Immutable;
using Fantactics.Core.Commands;
using Fantactics.Core.Maps;
using Fantactics.Core.State;

namespace Fantactics.Core.Engine;

/// <summary>
/// What one seat is allowed to see (Simulation §2). Bots only ever get this, never the full state. Hidden: the
/// opponent's pending orders, draft, unplaced units, and reserve composition (only its value is shown).
/// </summary>
/// <param name="Seat">The viewing seat.</param>
/// <param name="Turn">Current turn.</param>
/// <param name="Phase">Current phase.</param>
/// <param name="TiePriority">Seat that wins initiative ties this turn.</param>
/// <param name="Map">Current terrain.</param>
/// <param name="Units">The viewer's units in every location, plus enemy units on the field.</param>
/// <param name="Players">Public per-player totals.</param>
/// <param name="TurnState">Public turn bookkeeping (action order, held, braced, clash winners).</param>
/// <param name="MyPendingOrders">The viewer's own locked-in hidden orders, if any.</param>
/// <param name="PendingDecisions">Everything the game is waiting for (seats only; no hidden content).</param>
/// <param name="Outcome">Result once the match is over.</param>
public sealed record PlayerView(
    Seat Seat,
    int Turn,
    Phase Phase,
    Seat TiePriority,
    GameMap Map,
    ImmutableArray<Unit> Units,
    ImmutableSortedDictionary<Seat, PlayerSummary> Players,
    TurnState TurnState,
    ICommand? MyPendingOrders,
    ImmutableArray<Decision> PendingDecisions,
    MatchOutcome? Outcome)
{
    /// <summary>Projects the full state down to what <paramref name="seat"/> may see.</summary>
    public static PlayerView Project(GameState state, Seat seat)
    {
        ImmutableArray<Unit> units = state.Units.Values
            .Where(unit => unit.Owner == seat || unit.IsOnField)
            .ToImmutableArray();
        ImmutableSortedDictionary<Seat, PlayerSummary> players = state.Players.Values
            .ToImmutableSortedDictionary(
                player => player.Seat,
                player => new PlayerSummary(
                    player.Seat,
                    player.Race,
                    player.Command,
                    player.DestroyedValue,
                    player.ObjectivePoints,
                    UnitRules.ArmyValue(state, player.Seat),
                    UnitRules.ReserveValue(state, player.Seat),
                    state.PendingOrders.ContainsKey(player.Seat)));
        return new PlayerView(
            seat,
            state.Turn,
            state.Phase,
            state.TiePriority,
            state.Map,
            units,
            players,
            state.TurnState,
            state.PendingOrders.GetValueOrDefault(seat),
            GameEngine.PendingDecisions(state),
            state.Outcome);
    }
}
