using System.Collections.Immutable;
using Fantactics.Core.State;

namespace Fantactics.Core.Engine;

/// <summary>Consistency checks that must hold after every command (Simulation §7). Used by fuzz tests.</summary>
public static class Invariants
{
    /// <summary>Returns a description of every broken invariant; empty if the state is consistent.</summary>
    public static ImmutableArray<string> Check(GameState state)
    {
        List<string> problems = [];

        problems.AddRange(state.FieldUnits
            .GroupBy(unit => unit.Position)
            .Where(group => group.Count() > 1)
            .Select(group => $"Units {string.Join(", ", group.Select(u => u.Id))} share {group.Key}."));

        problems.AddRange(state.FieldUnits
            .Where(unit => !UnitRules.IsPassable(state, unit.Position))
            .Select(unit => $"Unit {unit.Id} stands on impassable or off-map tile {unit.Position}."));

        problems.AddRange(state.Units.Values
            .Where(unit => unit.Hp <= 0 || unit.Hp > state.DefinitionOf(unit).Hp)
            .Select(unit => $"Unit {unit.Id} has {unit.Hp} HP."));

        problems.AddRange(state.Players.Values
            .Where(player => player.Command < 0)
            .Select(player => $"{player.Seat} has negative Command ({player.Command})."));

        problems.AddRange(state.Units.Values
            .Where(unit => !state.Players.TryGetValue(unit.Owner, out PlayerState? owner) || owner.Eliminated)
            .Select(unit => $"Unit {unit.Id} belongs to {unit.Owner}, which isn't playing."));

        if (state.Phase != Phase.Over && state.LiveSeats.Select(state.TeamOf).Distinct().Count() < 2)
        {
            problems.Add("Fewer than two teams are playing, but the match isn't over.");
        }

        IEnumerable<int> queued = state.TurnState.ActionQueue.Concat(state.TurnState.DelayedQueue);
        problems.AddRange(queued
            .Where(id => !state.Units.TryGetValue(id, out Unit? unit) || !unit.IsOnField)
            .Select(id => $"Unit {id} is in the action order but not on the field."));
        problems.AddRange(queued
            .Where(state.TurnState.ClashWinners.Contains)
            .Select(id => $"Clash winner {id} is in the action order."));
        problems.AddRange(queued
            .GroupBy(id => id)
            .Where(group => group.Count() > 1)
            .Select(group => $"Unit {group.Key} appears twice in the action order."));

        if (state.Turn > state.Rules.TurnLimit)
        {
            problems.Add($"Turn {state.Turn} is past the turn limit {state.Rules.TurnLimit}.");
        }

        if ((state.Phase == Phase.Over) != (state.Outcome is not null))
        {
            problems.Add($"Phase {state.Phase} disagrees with the outcome {state.Outcome}.");
        }

        if (state.Phase == Phase.Action && state.TurnState.CurrentActor is null)
        {
            problems.Add("Action phase with nobody left to act.");
        }

        return [.. problems];
    }
}
