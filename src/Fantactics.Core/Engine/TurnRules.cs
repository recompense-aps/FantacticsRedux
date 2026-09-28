using System.Collections.Immutable;
using Fantactics.Core.Events;
using Fantactics.Core.State;

namespace Fantactics.Core.Engine;

/// <summary>Turn boundaries: Command income, status expiry, and the Deathmatch end conditions (GameDesign §4.5).</summary>
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
            foreach (Seat seat in SeatExtensions.All)
            {
                PlayerState player = state.Players[seat];
                int total = player.Command + state.Rules.CommandPerTurn;
                state = state.WithPlayer(player with { Command = total });
                events.Add(new CommandGained(seat, state.Rules.CommandPerTurn, total));
            }
        }

        return state;
    }

    /// <summary>Ends the current turn: expires statuses, checks Rout and the turn limit, and starts the next turn.</summary>
    public static GameState EndTurn(GameState state, List<GameEvent> events)
    {
        state = ScoreObjectives(state, events);
        events.Add(new TurnEnded(state.Turn));
        state = ExpireStatuses(state, events);

        MatchOutcome? outcome = CheckOutcome(state);
        if (outcome is not null)
        {
            events.Add(new MatchEnded(outcome.Winner, outcome.Reason));
            return state with { Phase = Phase.Over, Outcome = outcome, TurnState = TurnState.Empty };
        }

        return StartTurn(state, state.Turn + 1, state.TiePriority.Opponent(), events);
    }

    /// <summary>
    /// The player holding more objective tiles (a unit standing on them) scores the configured points
    /// (GameDesign §4.5). Holding the same number scores nothing, so each side must take the other's tiles.
    /// </summary>
    private static GameState ScoreObjectives(GameState state, List<GameEvent> events)
    {
        if (state.Rules.ObjectivePointsPerTurn <= 0 || state.Map.Objectives.IsEmpty)
        {
            return state;
        }

        Dictionary<Seat, int> held = SeatExtensions.All.ToDictionary(
            seat => seat,
            seat => state.Map.Objectives.Count(tile => state.UnitAt(tile)?.Owner == seat));
        if (held[Seat.P1] == held[Seat.P2])
        {
            return state;
        }

        Seat leader = held[Seat.P1] > held[Seat.P2] ? Seat.P1 : Seat.P2;
        PlayerState player = state.Players[leader];
        int total = player.ObjectivePoints + state.Rules.ObjectivePointsPerTurn;
        events.Add(new ObjectiveScored(
            leader,
            state.Rules.ObjectivePointsPerTurn,
            total,
            held[leader],
            held[leader.Opponent()]));
        return state.WithPlayer(player with { ObjectivePoints = total });
    }

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

    private static MatchOutcome? CheckOutcome(GameState state)
    {
        int threshold = state.Rules.DraftBudget * state.Rules.RoutPercent;
        Dictionary<Seat, int> values = SeatExtensions.All.ToDictionary(seat => seat, seat => UnitRules.ArmyValue(state, seat));
        List<Seat> routed = SeatExtensions.All.Where(seat => values[seat] * 100 < threshold).ToList();

        if (routed.Count == 2)
        {
            Seat? winner = values[Seat.P1] == values[Seat.P2]
                ? null
                : values[Seat.P1] > values[Seat.P2] ? Seat.P1 : Seat.P2;
            return new MatchOutcome(winner, EndReason.Rout);
        }

        if (routed.Count == 1)
        {
            return new MatchOutcome(routed[0].Opponent(), EndReason.Rout);
        }

        if (state.Turn >= state.Rules.TurnLimit)
        {
            int p1 = state.Players[Seat.P1].Score;
            int p2 = state.Players[Seat.P2].Score;
            Seat? winner = p1 == p2 ? null : p1 > p2 ? Seat.P1 : Seat.P2;
            return new MatchOutcome(winner, EndReason.TurnLimit);
        }

        return null;
    }
}
