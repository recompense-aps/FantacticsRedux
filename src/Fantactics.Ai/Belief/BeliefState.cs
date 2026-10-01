using System.Collections.Immutable;
using Fantactics.Core;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Geometry;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Ai.Belief;

/// <summary>
/// Rebuilds a plausible <see cref="GameState"/> from what a seat can see, so bots can drive the real engine for
/// lookahead without seeing hidden information. Hidden parts are filled with guesses: every other seat's reserve
/// composition and, where lookahead needs them, their orders.
/// </summary>
public static class BeliefState
{
    /// <summary>First id given to guessed units, well clear of real ids.</summary>
    public const int GuessedIdBase = 100_000;

    /// <summary>Builds a state matching <paramref name="view"/> in a one-against-one match.</summary>
    /// <param name="view">What the seat sees.</param>
    /// <param name="rules">Rules in effect (a view doesn't carry them).</param>
    /// <param name="enemyReserve">Guessed unit types of the opponent's reserve.</param>
    /// <exception cref="InvalidOperationException">The seat doesn't have exactly one opponent.</exception>
    public static GameState From(PlayerView view, RulesConfig rules, IReadOnlyList<string> enemyReserve) =>
        From(view, rules, new Dictionary<Seat, IReadOnlyList<string>> { [view.SoleOpponent()] = enemyReserve });

    /// <summary>Builds a state matching <paramref name="view"/>.</summary>
    /// <param name="view">What the seat sees.</param>
    /// <param name="rules">Rules in effect (a view doesn't carry them).</param>
    /// <param name="reserves">Guessed unit types of other seats' reserves, by seat.</param>
    public static GameState From(
        PlayerView view,
        RulesConfig rules,
        IReadOnlyDictionary<Seat, IReadOnlyList<string>> reserves)
    {
        IEnumerable<Unit> guessed = reserves
            .OrderBy(pair => pair.Key)
            .SelectMany(pair => pair.Value.Select(type => (Seat: pair.Key, Type: type)))
            .Select((guess, index) => Unit.Create(
                GuessedIdBase + index,
                guess.Seat,
                guess.Type,
                UnitLocation.Reserve,
                default(Point),
                rules.Units[guess.Type].Hp));
        ImmutableSortedDictionary<int, Unit> units = view.Units
            .Concat(guessed)
            .ToImmutableSortedDictionary(unit => unit.Id, unit => unit);

        ImmutableSortedDictionary<Seat, PlayerState> players = view.Players.Values.ToImmutableSortedDictionary(
            summary => summary.Seat,
            summary => new PlayerState(
                summary.Seat,
                summary.Command,
                summary.DestroyedValue,
                summary.ObjectivePoints,
                summary.AllowedRaces,
                summary.DraftedRaces,
                summary.DraftBudget,
                summary.StartingCap,
                summary.TeamNumber == summary.Seat.OwnTeam() ? null : summary.TeamNumber,
                summary.Eliminated ? view.Turn : null));

        ImmutableSortedDictionary<Seat, ICommand> pending = view.MyPendingOrders is ICommand mine
            ? ImmutableSortedDictionary<Seat, ICommand>.Empty.Add(view.Seat, mine)
            : ImmutableSortedDictionary<Seat, ICommand>.Empty;

        return new GameState(
            rules,
            view.Map,
            view.Turn,
            view.Phase,
            view.TiePriority,
            players,
            units,
            pending,
            view.TurnState,
            RngState: 0,
            NextUnitId: units.IsEmpty ? 1 : units.Keys.Max() + 1,
            view.Outcome);
    }
}
