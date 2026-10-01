using System.Collections.Immutable;
using Fantactics.Core.Geometry;
using Fantactics.Core.State;

namespace Fantactics.Core.Engine;

/// <summary>Finds where a unit can move this turn and the cheapest path to each tile.</summary>
public static class Pathfinder
{
    /// <summary>
    /// Every tile <paramref name="unit"/> can end its move on, with the cheapest path (ties broken deterministically).
    /// Paths may pass through friendly units but never enemies; tiles holding any other unit are excluded as
    /// destinations. Includes the minimum move (GameDesign §5).
    /// </summary>
    public static ImmutableArray<ReachableTile> Reachable(GameState state, Unit unit)
    {
        int budget = UnitRules.MovementPoints(state, unit);
        if (budget == 0)
        {
            return [];
        }

        HashSet<Point> enemyTiles = state.FieldUnits
            .Where(other => state.AreEnemies(other, unit))
            .Select(other => other.Position)
            .ToHashSet();
        HashSet<Point> occupied = state.FieldUnits.Select(other => other.Position).ToHashSet();

        Dictionary<Point, int> best = new() { [unit.Position] = 0 };
        Dictionary<Point, Point> previous = [];
        SortedSet<(int Cost, Point Tile)> frontier = [(0, unit.Position)];
        while (frontier.Count > 0)
        {
            (int cost, Point tile) = frontier.Min;
            frontier.Remove(frontier.Min);
            foreach (Point next in tile.Neighbors())
            {
                if (enemyTiles.Contains(next) || UnitRules.StepCost(state, unit, tile, next) is not int step)
                {
                    continue;
                }

                int total = cost + step;
                if (total > budget || (best.TryGetValue(next, out int known) && known <= total))
                {
                    continue;
                }

                if (best.TryGetValue(next, out int stale))
                {
                    frontier.Remove((stale, next));
                }

                best[next] = total;
                previous[next] = tile;
                frontier.Add((total, next));
            }
        }

        List<ReachableTile> reachable = best
            .Where(pair => pair.Key != unit.Position && !occupied.Contains(pair.Key))
            .Select(pair => new ReachableTile(pair.Key, pair.Value, BuildPath(previous, unit.Position, pair.Key)))
            .ToList();

        IEnumerable<ReachableTile> minimumMoves = unit.Position.Neighbors()
            .Where(next => !best.ContainsKey(next) && !occupied.Contains(next))
            .Select(next => (Tile: next, Cost: UnitRules.StepCost(state, unit, unit.Position, next)))
            .Where(step => step.Cost is not null)
            .Select(step => new ReachableTile(step.Tile, step.Cost ?? 0, [step.Tile]));

        return reachable
            .Concat(minimumMoves)
            .OrderBy(tile => tile.Tile)
            .ToImmutableArray();
    }

    /// <summary>Cheapest path for <paramref name="unit"/> to <paramref name="destination"/>, if reachable.</summary>
    public static ImmutableArray<Point>? PathTo(GameState state, Unit unit, Point destination) =>
        Reachable(state, unit).FirstOrDefault(tile => tile.Tile == destination)?.Path;

    /// <summary>
    /// Cheapest path between any two tiles using <paramref name="unit"/>'s move costs, ignoring its Movement budget
    /// (for building multi-waypoint orders). Avoids tiles enemies stand on, except <paramref name="to"/> itself.
    /// </summary>
    /// <returns>Tiles after <paramref name="from"/> up to and including <paramref name="to"/>, or <c>null</c>.</returns>
    public static ImmutableArray<Point>? CheapestPath(GameState state, Unit unit, Point from, Point to)
    {
        HashSet<Point> enemyTiles = state.FieldUnits
            .Where(other => state.AreEnemies(other, unit) && other.Position != to)
            .Select(other => other.Position)
            .ToHashSet();
        Dictionary<Point, int> best = new() { [from] = 0 };
        Dictionary<Point, Point> previous = [];
        SortedSet<(int Cost, Point Tile)> frontier = [(0, from)];
        while (frontier.Count > 0)
        {
            (int cost, Point tile) = frontier.Min;
            frontier.Remove(frontier.Min);
            if (tile == to)
            {
                return from == to ? [] : BuildPath(previous, from, to);
            }

            foreach (Point next in tile.Neighbors())
            {
                if (enemyTiles.Contains(next) || UnitRules.StepCost(state, unit, tile, next) is not int step)
                {
                    continue;
                }

                int total = cost + step;
                if (best.TryGetValue(next, out int known) && known <= total)
                {
                    continue;
                }

                if (best.TryGetValue(next, out int stale))
                {
                    frontier.Remove((stale, next));
                }

                best[next] = total;
                previous[next] = tile;
                frontier.Add((total, next));
            }
        }

        return null;
    }

    private static ImmutableArray<Point> BuildPath(Dictionary<Point, Point> previous, Point start, Point end)
    {
        List<Point> path = [];
        for (Point current = end; current != start; current = previous[current])
        {
            path.Add(current);
        }

        path.Reverse();
        return [.. path];
    }
}
