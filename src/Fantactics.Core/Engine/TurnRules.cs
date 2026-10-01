using System.Collections.Immutable;
using Fantactics.Core.Events;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Core.Engine;

/// <summary>
/// Turn boundaries: Command income, status expiry, tie-priority rotation, and the Deathmatch end conditions and
/// elimination (GameDesign §4.1, §4.5).
/// </summary>
internal static class TurnRules
{
    /// <summary>Begins <paramref name="turn"/>: grants Command and opens the movement phase.</summary>
    public static GameState StartTurn(GameState state, int turn, Seat tiePriority, List<GameEvent> events)
    {
        state = state with
        {
            Turn = turn,
            Phase = Phase.Movement,
            TiePriority = tiePriority,
            TurnState = TurnState.Empty,
        };
        events.Add(new TurnStarted(turn, tiePriority));

        if (turn >= state.Rules.CommandStartTurn)
        {
            foreach (Seat seat in state.LiveSeats.ToList())
            {
                PlayerState player = state.Players[seat];
                int total = player.Command + state.Rules.CommandPerTurn;
                state = state.WithPlayer(player with { Command = total });
                events.Add(new CommandGained(seat, state.Rules.CommandPerTurn, total));
            }
        }

        return state;
    }

    /// <summary>
    /// Ends the current turn: expires statuses, checks Rout (eliminating routed seats while other teams play on) and
    /// the turn limit, and starts the next turn.
    /// </summary>
    public static GameState EndTurn(GameState state, List<GameEvent> events)
    {
        state = ScoreObjectives(state, events);
        events.Add(new TurnEnded(state.Turn));
        state = ExpireStatuses(state, events);

        (state, MatchOutcome? outcome) = CheckOutcome(state, events);
        if (outcome is not null)
        {
            events.Add(new MatchEnded(outcome.Winners, outcome.Reason));
            return state with { Phase = Phase.Over, Outcome = outcome, TurnState = TurnState.Empty };
        }

        return StartTurn(state, state.Turn + 1, NextTiePriority(state), events);
    }

    /// <summary>
    /// The seat after the current tie-priority seat, in seat order, that is still playing (GameDesign §4.1). With two
    /// seats, priority alternates.
    /// </summary>
    private static Seat NextTiePriority(GameState state)
    {
        List<Seat> seats = [.. state.Seats];
        int current = seats.IndexOf(state.TiePriority);
        return Enumerable.Range(1, seats.Count)
            .Select(offset => seats[(current + offset) % seats.Count])
            .First(seat => !state.Players[seat].Eliminated);
    }

    /// <summary>
    /// Scores objective tiles held by a unit standing on them (GameDesign §4.5). With
    /// <see cref="ObjectiveScoring.Majority"/>, the one player holding the most tiles scores the configured points,
    /// and a tie for the most scores nothing; with <see cref="ObjectiveScoring.PerTile"/>, each player scores the
    /// points per tile. Eliminated players don't score.
    /// With <see cref="RulesConfig.ObjectivesNeedHold"/>, only units that didn't move this turn hold a tile.
    /// </summary>
    private static GameState ScoreObjectives(GameState state, List<GameEvent> events)
    {
        if (state.Rules.ObjectivePointsPerTurn <= 0 || state.Map.Objectives.IsEmpty)
        {
            return state;
        }

        Dictionary<Seat, int> held = state.LiveSeats.ToDictionary(
            seat => seat,
            seat => state.Map.Objectives.Count(tile => state.UnitAt(tile) is Unit unit
                && unit.Owner == seat
                && HoldsObjective(state, unit)));
        int most = held.Values.Max();
        List<Seat> leaders = held.Keys.Where(seat => held[seat] == most).ToList();
        IEnumerable<(Seat Seat, int Points)> scores = state.Rules.ObjectiveScoring switch
        {
            ObjectiveScoring.PerTile => held.Keys
                .Where(seat => held[seat] > 0)
                .Select(seat => (seat, held[seat] * state.Rules.ObjectivePointsPerTurn)),
            _ when most > 0 && leaders is [Seat leader] => [(leader, state.Rules.ObjectivePointsPerTurn)],
            _ => [],
        };

        foreach ((Seat seat, int points) in scores.ToList())
        {
            PlayerState player = state.Players[seat];
            int total = player.ObjectivePoints + points;
            int enemyHeld = state.Opponents(seat).Select(enemy => held[enemy]).DefaultIfEmpty(0).Max();
            events.Add(new ObjectiveScored(seat, points, total, held[seat], enemyHeld));
            state = state.WithPlayer(player with { ObjectivePoints = total });
        }

        return state;
    }

    /// <summary>Whether <paramref name="unit"/>, standing on an objective, holds it for scoring this turn.</summary>
    private static bool HoldsObjective(GameState state, Unit unit) =>
        !state.Rules.ObjectivesNeedHold
        || state.TurnState.Held.Contains(unit.Id)
        || (unit.Has(StatusKind.Rooted) && !state.TurnState.ClashWinners.Contains(unit.Id));

    private static GameState ExpireStatuses(GameState state, List<GameEvent> events)
    {
        foreach (Unit unit in state.Units.Values.Where(unit => !unit.Statuses.IsEmpty).ToList())
        {
            List<StatusKind> expired = unit.Statuses
                .Where(pair => pair.Value <= state.Turn)
                .Select(pair => pair.Key)
                .ToList();
            if (expired.Count == 0)
            {
                continue;
            }

            ImmutableSortedDictionary<StatusKind, int> remaining = unit.Statuses.RemoveRange(expired);
            state = state.WithUnit(unit with { Statuses = remaining });
            events.AddRange(expired.Select(status => new StatusRemoved(unit.Id, status)));
        }

        return state;
    }

    /// <summary>
    /// Checks Rout and the turn limit (GameDesign §4.5). Routed seats are eliminated while at least two teams would
    /// still be standing; otherwise the match ends there and nothing is removed. A team left standing alone wins; if
    /// every team routs at once, the one with the most army value left wins. At the turn limit, the team with the
    /// highest total score wins. Equal best teams draw.
    /// </summary>
    private static (GameState State, MatchOutcome? Outcome) CheckOutcome(GameState state, List<GameEvent> events)
    {
        List<Seat> live = [.. state.LiveSeats];
        Dictionary<Seat, int> values = live.ToDictionary(seat => seat, seat => UnitRules.ArmyValue(state, seat));
        List<Seat> routed = live
            .Where(seat => values[seat] * 100 < state.Players[seat].BudgetUnder(state.Rules) * state.Rules.RoutPercent)
            .ToList();
        HashSet<int> standing = live
            .Except(routed)
            .Select(state.TeamOf)
            .ToHashSet();

        if (routed.Count > 0 && standing.Count < 2)
        {
            return (state, Decide(
                state,
                EndReason.Rout,
                seats => (standing.Contains(state.TeamOf(seats[0])) ? 1 : 0, seats.Sum(values.GetValueOrDefault))));
        }

        state = routed.Aggregate(state, (current, seat) => Eliminate(current, seat, events));
        return state.Turn >= state.Rules.TurnLimit
            ? (state, Decide(state, EndReason.TurnLimit, seats => (0, seats.Sum(seat => state.Players[seat].Score))))
            : (state, null);
    }

    /// <summary>
    /// Ranks the teams and builds the outcome. Teams with a live seat are ranked by <paramref name="rank"/> (higher is
    /// better) and always above fully eliminated teams, which rank by how late they went out.
    /// </summary>
    /// <param name="state">The final state.</param>
    /// <param name="reason">Why the match ended.</param>
    /// <param name="rank">A team's rank from all its seats, in seat order.</param>
    private static MatchOutcome Decide(GameState state, EndReason reason, Func<List<Seat>, (int, int)> rank)
    {
        Dictionary<int, List<Seat>> teams = state.Seats
            .GroupBy(state.TeamOf)
            .ToDictionary(team => team.Key, team => team.ToList());
        Dictionary<int, (int OutTurn, int Primary, int Secondary)> keys = teams.ToDictionary(
            team => team.Key,
            team =>
            {
                bool alive = team.Value.Any(seat => !state.Players[seat].Eliminated);
                (int primary, int secondary) = alive ? rank(team.Value) : (0, 0);
                int outTurn = alive
                    ? int.MaxValue
                    : team.Value.Max(seat => state.Players[seat].EliminatedOnTurn ?? 0);
                return (outTurn, primary, secondary);
            });
        ImmutableSortedDictionary<Seat, int> placings = teams
            .SelectMany(team => team.Value.Select(seat => KeyValuePair.Create(
                seat,
                1 + keys.Values.Count(other => other.CompareTo(keys[team.Key]) > 0))))
            .ToImmutableSortedDictionary();
        List<int> best = keys.Keys
            .Where(team => keys.Values.All(other => other.CompareTo(keys[team]) <= 0))
            .ToList();
        ImmutableArray<Seat> winners = best is [int winner] ? [.. teams[winner]] : [];
        return new MatchOutcome(winners, reason, placings);
    }

    /// <summary>Removes a routed seat from a match that goes on without it (GameDesign §4.5).</summary>
    private static GameState Eliminate(GameState state, Seat seat, List<GameEvent> events)
    {
        List<Unit> units = state.Units.Values
            .Where(unit => unit.Owner == seat)
            .ToList();
        ImmutableArray<int> field = [.. units.Where(unit => unit.IsOnField).Select(unit => unit.Id)];
        state = state with { Units = state.Units.RemoveRange(units.Select(unit => unit.Id)) };
        events.Add(new SeatEliminated(seat, field));
        return state.WithPlayer(state.Players[seat] with { EliminatedOnTurn = state.Turn });
    }
}
