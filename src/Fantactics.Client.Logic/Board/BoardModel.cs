using System.Collections.Immutable;
using Fantactics.Client.Logic.Input;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Geometry;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Client.Logic.Board;

/// <summary>
/// Everything the board draws besides terrain (unit tokens, tile highlights, order arrows), a hint for the hovered
/// tile, and the unit the info panel describes. A pure function of the seat's view and the input state, rebuilt whenever either changes.
/// </summary>
/// <param name="Tokens">Units on the field.</param>
/// <param name="Marks">Highlighted tiles, including the enemy threats shown (<see cref="ThreatOverlay"/>).</param>
/// <param name="Arrows">Move and deploy orders to draw.</param>
/// <param name="Hint">What hovering the tile would do or shows, if anything.</param>
/// <param name="Info">
/// The unit the info panel describes: the hovered one, else the one selected for orders or placement, else the one
/// acting; <c>null</c> for none.
/// </param>
public sealed record BoardModel(
    ImmutableArray<TokenModel> Tokens,
    ImmutableDictionary<Point, TileMark> Marks,
    ImmutableArray<OrderArrow> Arrows,
    string? Hint,
    UnitInfo? Info = null)
{
    /// <summary>Builds the model.</summary>
    /// <param name="view">The seat's view.</param>
    /// <param name="rules">Rules, for unit stats.</param>
    /// <param name="moves">Orders being built in the movement phase, if any.</param>
    /// <param name="actions">The action being picked in the action phase, if any.</param>
    /// <param name="hover">The tile under the pointer, if any.</param>
    /// <param name="placement">The starting placement being built, if any.</param>
    /// <param name="threats">
    /// Whether to show every enemy's threats. A hovered enemy's threats are shown either way, and only theirs.
    /// </param>
    public static BoardModel Build(
        PlayerView view,
        RulesConfig rules,
        MoveOrderBuilder? moves,
        ActionPicker? actions,
        Point? hover,
        PlacementBuilder? placement = null,
        bool threats = false)
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

        ThreatOverlay threat = ThreatsFor(view, rules, tokens, hover, threats);
        Mark(threat.Move, TileMark.ThreatMove);
        Mark(threat.Attack, TileMark.ThreatAttack);
        Mark(tokens.Where(t => t.Acting && t.Mine).Select(t => t.Tile), TileMark.Selected);
        if (moves is not null)
        {
            Mark(moves.Selected is null ? moves.ArrivalTiles : [], TileMark.Arrival);
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

        string? hint = HintFor(view, rules, actions, hover);
        if (hover is Point hovered && marks.GetValueOrDefault(hovered).HasFlag(TileMark.Arrival))
        {
            hint += " · reserve arrival tile: pick a unit under Deploy";
        }

        return new BoardModel(
            tokens,
            marks.ToImmutableDictionary(),
            ArrowsFor(view, moves),
            hint,
            InfoFor(view, rules, tokens, moves, placement, hover));
    }

    /// <summary>The hovered enemy's threats, else every enemy's when <paramref name="all"/> is set, else none.</summary>
    private static ThreatOverlay ThreatsFor(
        PlayerView view,
        RulesConfig rules,
        ImmutableArray<TokenModel> tokens,
        Point? hover,
        bool all)
    {
        IEnumerable<TokenModel> enemies = tokens.Where(token => view.AreEnemies(token.Owner, view.Seat));
        List<int> hovered = enemies
            .Where(token => token.Tile == hover)
            .Select(token => token.Id)
            .ToList();
        IEnumerable<int> shown = hovered.Count > 0 ? hovered : all ? enemies.Select(token => token.Id) : [];
        return ThreatOverlay.For(view, rules, shown);
    }

    /// <summary>Describes the hovered unit, else the selected one, else the acting one (the viewer's first).</summary>
    private static UnitInfo? InfoFor(
        PlayerView view,
        RulesConfig rules,
        ImmutableArray<TokenModel> tokens,
        MoveOrderBuilder? moves,
        PlacementBuilder? placement,
        Point? hover)
    {
        int? hovered = hover is Point tile
            ? tokens
                .Where(token => token.Tile == tile)
                .Select(token => (int?)token.Id)
                .FirstOrDefault()
            : null;
        int? acting = view.PendingDecisions
            .OfType<ChooseUnitActionDecision>()
            .OrderBy(decision => decision.Seat != view.Seat)
            .Select(decision => (int?)decision.UnitId)
            .FirstOrDefault();
        int? shown = hovered ?? moves?.Selected ?? placement?.Selected ?? acting;
        return view.Units.FirstOrDefault(unit => unit.Id == shown) is Unit unit ? UnitInfo.Of(view, rules, unit) : null;
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
