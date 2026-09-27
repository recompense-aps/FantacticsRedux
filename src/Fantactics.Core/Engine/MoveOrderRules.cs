using Fantactics.Core.Commands;
using Fantactics.Core.Geometry;
using Fantactics.Core.Maps;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Core.Engine;

/// <summary>Validation of move and deploy orders at submit time (GameDesign §4.1, §4.4, §5).</summary>
internal static class MoveOrderRules
{
    /// <summary>Throws a <see cref="RuleViolationException"/> unless all of the seat's orders are legal together.</summary>
    public static void Validate(GameState state, Seat seat, SubmitMoveOrders orders)
    {
        RuleViolationException.ThrowUnless(
            orders.Moves.Select(move => move.UnitId).Distinct().Count() == orders.Moves.Length,
            "duplicate-order",
            "Each unit can have at most one move order.");

        foreach (MoveOrder move in orders.Moves)
        {
            ValidateMove(state, seat, move);
        }

        HashSet<int> moving = orders.Moves
            .Where(move => !move.Path.IsEmpty)
            .Select(move => move.UnitId)
            .ToHashSet();
        foreach (MoveOrder move in orders.Moves.Where(move => !move.Path.IsEmpty))
        {
            Unit? occupant = state.UnitAt(move.Path[^1]);
            RuleViolationException.ThrowUnless(
                occupant is null || occupant.Owner != seat || moving.Contains(occupant.Id),
                "destination-held",
                $"Unit {move.UnitId} can't end on {move.Path[^1]}: unit {occupant?.Id} holds there.");
        }

        ValidateDeploys(state, seat, orders.Deploys);
    }

    /// <summary>Throws a <see cref="RuleViolationException"/> unless one unit's move order is legal on its own.</summary>
    public static void ValidateMove(GameState state, Seat seat, MoveOrder move)
    {
        RuleViolationException.ThrowUnless(
            state.Units.TryGetValue(move.UnitId, out Unit? unit) && unit.Owner == seat && unit.IsOnField,
            "not-your-unit",
            $"Unit {move.UnitId} is not one of your units on the field.");
        if (move.Path.IsEmpty)
        {
            return;
        }

        RuleViolationException.ThrowUnless(
            !unit.Has(StatusKind.Rooted),
            "rooted",
            $"Unit {unit.Id} is Rooted and must hold.");

        HashSet<Point> visited = [unit.Position];
        Point previous = unit.Position;
        int cost = 0;
        foreach (Point step in move.Path)
        {
            RuleViolationException.ThrowUnless(
                step.IsAdjacentTo(previous) && visited.Add(step),
                "bad-path",
                $"Unit {unit.Id}'s path must step orthogonally onto new tiles ({previous} to {step}).");

            // Paths may enter a tile an enemy stands on now: orders are simultaneous, so the enemy may leave it (or
            // swap into this unit, which is a clash). Resolution blocks the step if the enemy stays.
            int? stepCost = UnitRules.StepCost(state, unit, previous, step);
            RuleViolationException.ThrowUnless(stepCost is not null, "impassable", $"{step} is impassable.");
            cost += stepCost ?? 0;
            previous = step;
        }

        int budget = UnitRules.MovementPoints(state, unit);
        RuleViolationException.ThrowUnless(
            move.Path.Length == 1 || cost <= budget,
            "too-far",
            $"Unit {unit.Id}'s path costs {cost}; it has {budget} Movement.");
    }

    /// <summary>Whether <paramref name="unit"/> may arrive on <paramref name="tile"/> this turn (GameDesign §4.4).</summary>
    public static bool IsDeployTile(GameState state, Seat seat, Unit unit, Point tile)
    {
        if (!UnitRules.IsPassable(state, tile)
            || state.UnitAt(tile) is not null
            || UnitRules.AdjacentEnemies(state, seat, tile).Any())
        {
            return false;
        }

        int columns = state.Rules.DeployColumns;
        if (state.Map.IsInDeployZone(tile, seat, columns))
        {
            return true;
        }

        Terrain terrain = state.Map[tile];
        bool fromTheTrees = terrain == Terrain.Forest
            && UnitRules.HasTrait(state, unit, TraitIds.FromTheTrees)
            && !state.FieldUnits.Any(other => other.Owner != seat && other.Position.DistanceTo(tile) <= 2);
        bool outOfTheCaves = terrain == Terrain.Mountains
            && UnitRules.HasTrait(state, unit, TraitIds.OutOfTheCaves)
            && state.Map.IsInOrNextToDeployZone(tile, seat, columns);
        return fromTheTrees || outOfTheCaves;
    }

    private static void ValidateDeploys(GameState state, Seat seat, IReadOnlyCollection<DeployOrder> deploys)
    {
        RuleViolationException.ThrowUnless(
            deploys.Count <= state.Rules.MaxArrivalsPerTurn,
            "too-many-arrivals",
            $"At most {state.Rules.MaxArrivalsPerTurn} units can arrive per turn.");
        RuleViolationException.ThrowUnless(
            deploys.Select(d => d.UnitId).Distinct().Count() == deploys.Count
                && deploys.Select(d => d.Tile).Distinct().Count() == deploys.Count,
            "duplicate-deploy",
            "Each deploy needs its own unit and its own tile.");

        int totalCost = 0;
        foreach (DeployOrder deploy in deploys)
        {
            RuleViolationException.ThrowUnless(
                state.Units.TryGetValue(deploy.UnitId, out Unit? unit)
                    && unit.Owner == seat
                    && unit.Location == UnitLocation.Reserve,
                "not-in-reserve",
                $"Unit {deploy.UnitId} is not in your reserve.");
            RuleViolationException.ThrowUnless(
                IsDeployTile(state, seat, unit, deploy.Tile),
                "bad-deploy-tile",
                $"Unit {unit.Id} can't arrive on {deploy.Tile}.");
            totalCost += UnitRules.DeployCost(state, unit);
        }

        int command = state.Players[seat].Command;
        RuleViolationException.ThrowUnless(
            totalCost <= command,
            "not-enough-command",
            $"Deploying costs {totalCost} Command; you have {command}.");
    }
}
