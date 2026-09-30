using System.Collections.Immutable;
using Fantactics.Client.Logic.Input;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Geometry;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Client.Logic.Board;

/// <summary>
/// Everything the board draws besides terrain: unit tokens, tile highlights, order arrows, and a hint for the
/// hovered tile. A pure function of the seat's view and the input state, rebuilt whenever either changes.
/// </summary>
/// <param name="Tokens">Units on the field.</param>
/// <param name="Marks">Highlighted tiles.</param>
/// <param name="Arrows">Move and deploy orders to draw.</param>
/// <param name="Hint">What hovering the tile would do or shows, if anything.</param>
public sealed record BoardModel(
    ImmutableArray<TokenModel> Tokens,
    ImmutableDictionary<Point, TileMark> Marks,
    ImmutableArray<OrderArrow> Arrows,
    string? Hint)
{
    /// <summary>Builds the model.</summary>
    /// <param name="view">The seat's view.</param>
    /// <param name="rules">Rules, for unit stats.</param>
    /// <param name="moves">Orders being built in the movement phase, if any.</param>
    /// <param name="actions">The action being picked in the action phase, if any.</param>
    /// <param name="hover">The tile under the pointer, if any.</param>
    /// <param name="placement">The starting placement being built, if any.</param>
    public static BoardModel Build(
        PlayerView view,
        RulesConfig rules,
        MoveOrderBuilder? moves,
        ActionPicker? actions,
        Point? hover,
        PlacementBuilder? placement = null)
    {
        ImmutableHashSet<int> acting = [.. view.PendingDecisions.OfType<ChooseUnitActionDecision>().Select(d => d.UnitId)];
        ImmutableArray<TokenModel> tokens = [.. view.Units
            .Where(unit => unit.IsOnField)
            .Select(unit => new TokenModel(
                unit.Id,
                unit.Owner,
                unit.Type,
                unit.Position,
                unit.Hp,
                rules.Units[unit.Type].Hp,
                unit.Owner == view.Seat,
                view.TurnState.Held.Contains(unit.Id),
                view.TurnState.Braced.Contains(unit.Id),
                [.. unit.Statuses.Keys],
                acting.Contains(unit.Id))), .. PlacedTokens(view, rules, placement)];

        Dictionary<Point, TileMark> marks = view.Map.Objectives.ToDictionary(tile => tile, _ => TileMark.Objective);
        void Mark(IEnumerable<Point> tiles, TileMark mark)
        {
            foreach (Point tile in tiles)
            {
                marks[tile] = marks.GetValueOrDefault(tile) | mark;
            }
        }

        Mark(tokens.Where(t => t.Acting && t.Mine).Select(t => t.Tile), TileMark.Selected);
        if (moves is not null)
        {
            Mark(moves.Targets, TileMark.Reachable);
            Mark(tokens.Where(t => t.Id == moves.Selected && t.Mine).Select(t => t.Tile), TileMark.Selected);
            Mark(hover is Point tile ? moves.Preview(tile) : [], TileMark.Path);
        }

        if (placement is not null)
        {
            Mark(placement.FreeTiles, TileMark.Reachable);
            if (placement.Selected is int selected && placement.Placed.TryGetValue(selected, out Point at))
            {
                Mark([at], TileMark.Selected);
            }
        }

        if (actions is not null)
        {
            Mark(actions.Targets, TileMark.Target);
        }

        return new BoardModel(tokens, marks.ToImmutableDictionary(), ArrowsFor(view, moves), HintFor(view, rules, actions, hover));
    }

    /// <summary>Starting units placed so far (or already submitted), which aren't on the field yet.</summary>
    private static IEnumerable<TokenModel> PlacedTokens(PlayerView view, RulesConfig rules, PlacementBuilder? placement)
    {
        IEnumerable<(int UnitId, Point Tile)> placed = placement is not null
            ? placement.Placed.Select(pair => (pair.Key, pair.Value))
            : view.MyPendingOrders is PlaceStartingArmy submitted
                ? submitted.Placements.Select(p => (p.UnitId, p.Tile))
                : [];
        Dictionary<int, Unit> units = view.Units.ToDictionary(unit => unit.Id);
        return placed
            .Where(p => units.ContainsKey(p.UnitId))
            .Select(p => new TokenModel(
                p.UnitId,
                view.Seat,
                units[p.UnitId].Type,
                p.Tile,
                units[p.UnitId].Hp,
                rules.Units[units[p.UnitId].Type].Hp,
                Mine: true,
                Held: false,
                Braced: false,
                [],
                Acting: false));
    }

    private static ImmutableArray<OrderArrow> ArrowsFor(PlayerView view, MoveOrderBuilder? moves)
    {
        SubmitMoveOrders? orders = moves?.Build() ?? view.MyPendingOrders as SubmitMoveOrders;
        if (orders is null)
        {
            return [];
        }

        Dictionary<int, Point> positions = view.Units.Where(u => u.IsOnField).ToDictionary(u => u.Id, u => u.Position);
        IEnumerable<OrderArrow> paths = orders.Moves
            .Where(move => !move.Path.IsEmpty && positions.ContainsKey(move.UnitId))
            .Select(move => new OrderArrow(move.UnitId, [positions[move.UnitId], .. move.Path], false));
        IEnumerable<OrderArrow> arrivals = orders.Deploys.Select(deploy => new OrderArrow(deploy.UnitId, [deploy.Tile], true));
        return [.. paths.Concat(arrivals)];
    }

    private static string? HintFor(PlayerView view, RulesConfig rules, ActionPicker? actions, Point? hover)
    {
        if (hover is not Point tile || !view.Map.Contains(tile))
        {
            return null;
        }

        if (actions?.OptionAt(tile) is ActionOption { Preview: AttackPreview preview })
        {
            return $"Attack: {preview.Damage} damage" + (preview.Kills ? ", kills" : "");
        }

        string terrain = view.Map[tile].ToString();
        return view.Units.FirstOrDefault(u => u.IsOnField && u.Position == tile) is Unit unit
            ? $"{unit.Type} ({unit.Owner}) {unit.Hp}/{rules.Units[unit.Type].Hp} HP on {terrain}"
                + (unit.Statuses.IsEmpty ? "" : $", {string.Join(", ", unit.Statuses.Keys)}")
            : terrain;
    }
}
