using System.Collections.Immutable;
using Fantactics.Core.Commands;
using Fantactics.Core.Maps;
using Fantactics.Core.State;

namespace Fantactics.Core.Engine;

/// <summary>
/// What one seat is allowed to see (Simulation §2). Bots only ever get this, never the full state. Hidden: every
/// other seat's pending orders, draft, unplaced units, and reserve composition (only its value is shown, plus the
/// unit count per race once every draft is in: the draft reveal, GameDesign §4.4), teammates included. Units are
/// named with the seat's own ids (<see cref="ViewIds"/>), never engine ids, which would reveal draft sizes.
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
    /// <summary>Whether the players in two seats are on different teams. A seat is never its own enemy.</summary>
    public bool AreEnemies(Seat a, Seat b) => a != b && Players[a].TeamNumber != Players[b].TeamNumber;

    /// <summary>Seats on other teams than the viewer that are still playing, in seat order.</summary>
    public IEnumerable<Seat> Opponents() =>
        Players.Values
            .Where(player => !player.Eliminated && AreEnemies(Seat, player.Seat))
            .Select(player => player.Seat);

    /// <summary>The viewer's only opponent, for text and tools that only make sense one against one.</summary>
    /// <exception cref="InvalidOperationException">The viewer has more than one opponent, or none.</exception>
    public Seat SoleOpponent() => Opponents().ToList() is [Seat only]
        ? only
        : throw new InvalidOperationException($"{Seat} doesn't have exactly one opponent.");

    /// <summary>Projects the full state down to what <paramref name="seat"/> may see.</summary>
    public static PlayerView Project(GameState state, Seat seat)
    {
        ViewIds ids = ViewIds.For(state, seat);
        ImmutableArray<Unit> units = state.Units.Values
            .Where(unit => unit.Owner == seat || unit.IsOnField)
            .Select(ids.ToView)
            .OrderBy(unit => unit.Id)
            .ToImmutableArray();
        ImmutableSortedDictionary<Seat, PlayerSummary> players = state.Players.Values
            .ToImmutableSortedDictionary(
                player => player.Seat,
                player => new PlayerSummary(
                    player.Seat,
                    player.Command,
                    player.DestroyedValue,
                    player.ObjectivePoints,
                    UnitRules.ArmyValue(state, player.Seat),
                    UnitRules.ReserveValue(state, player.Seat),
                    state.PendingOrders.ContainsKey(player.Seat),
                    player.AllowedRaces,
                    player.DraftedRaces,
                    player.BudgetUnder(state.Rules),
                    player.StartingCapUnder(state.Rules),
                    player.TeamNumber,
                    player.Eliminated));
        return new PlayerView(
            seat,
            state.Turn,
            state.Phase,
            state.TiePriority,
            state.Map,
            units,
            players,
            ids.ToView(state.TurnState),
            state.PendingOrders.GetValueOrDefault(seat) is ICommand mine ? ids.ToView(mine) : null,
            [.. GameEngine.PendingDecisions(state).Select(ids.ToView)],
            state.Outcome);
    }
}
