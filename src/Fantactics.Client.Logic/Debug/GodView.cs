using System.Collections.Immutable;
using Fantactics.Client.Logic.Board;
using Fantactics.Core;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Geometry;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Client.Logic.Debug;

/// <summary>
/// The board as the full state has it, for the debug toggle: every unit with its engine id, and both seats' locked-in
/// move orders as arrows. Never shown to a player in a real match.
/// </summary>
public static class GodView
{
    /// <summary>Builds the board model.</summary>
    /// <param name="state">The full state.</param>
    /// <param name="shown">The seat whose units count as "mine".</param>
    /// <param name="hover">The tile under the pointer, if any.</param>
    public static BoardModel Build(GameState state, Seat shown, Point? hover)
    {
        RulesConfig rules = state.Rules;
        ImmutableHashSet<int> acting = [.. GameEngine.PendingDecisions(state).OfType<ChooseUnitActionDecision>().Select(d => d.UnitId)];
        ImmutableArray<TokenModel> tokens = [.. state.FieldUnits.Select(unit => new TokenModel(
            unit.Id,
            unit.Owner,
            unit.Type,
            unit.Position,
            unit.Hp,
            rules.Units[unit.Type].Hp,
            unit.Owner == shown,
            state.TurnState.Held.Contains(unit.Id),
            state.TurnState.Braced.Contains(unit.Id),
            [.. unit.Statuses.Keys],
            acting.Contains(unit.Id)))];
        ImmutableArray<OrderArrow> arrows = [.. state.PendingOrders.Values
            .OfType<SubmitMoveOrders>()
            .SelectMany(orders => orders.Moves
                .Where(move => !move.Path.IsEmpty && state.Units.ContainsKey(move.UnitId))
                .Select(move => new OrderArrow(move.UnitId, [state.Units[move.UnitId].Position, .. move.Path], false))
                .Concat(orders.Deploys.Select(deploy => new OrderArrow(deploy.UnitId, [deploy.Tile], true))))];
        string? hint = hover is Point tile && state.Map.Contains(tile)
            ? state.UnitAt(tile) is Unit unit
                ? $"#{unit.Id} {unit.Type} ({unit.Owner}) {unit.Hp}/{rules.Units[unit.Type].Hp} HP on {state.Map[tile]}"
                : state.Map[tile].ToString()
            : null;
        ImmutableDictionary<Point, TileMark> marks = state.Map.Objectives.ToImmutableDictionary(t => t, _ => TileMark.Objective);
        UnitInfo? info = hover is Point at && state.Map.Contains(at) && state.UnitAt(at) is Unit hovered
            ? UnitInfo.Of(
                hovered,
                rules,
                state.Turn,
                state.TurnState,
                hovered.Owner == shown ? "yours" : state.AreEnemies(hovered.Owner, shown) ? "enemy" : "ally")
            : null;
        return new BoardModel(tokens, marks, arrows, hint, info);
    }
}
