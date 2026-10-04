using Fantactics.Core.Geometry;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Core.Engine;

/// <summary>Where a unit could move and strike next turn, for threat maps and the client's threat overlay.</summary>
public static class ThreatRange
{
    /// <summary>Tiles <paramref name="unit"/> can end its move on (<see cref="Pathfinder.Reachable"/>).</summary>
    public static IReadOnlySet<Point> Reach(GameState state, Unit unit) =>
        Pathfinder.Reachable(state, unit)
            .Select(reachable => reachable.Tile)
            .ToHashSet();

    /// <summary>
    /// Tiles <paramref name="unit"/> could hit with a basic attack: every tile in attack range (and line of sight) of
    /// every tile it can move to, or of where it stands. Ability ranges aren't included.
    /// </summary>
    public static IReadOnlySet<Point> Strike(GameState state, Unit unit)
    {
        UnitDefinition definition = state.DefinitionOf(unit);
        IEnumerable<Point> origins = Pathfinder.Reachable(state, unit)
            .Select(reachable => reachable.Tile)
            .Prepend(unit.Position);
        HashSet<Point> tiles = [];
        foreach (Point origin in origins)
        {
            Unit placed = unit with { Position = origin };
            for (int dx = -definition.MaxRange; dx <= definition.MaxRange; dx++)
            {
                int reach = definition.MaxRange - Math.Abs(dx);
                for (int dy = -reach; dy <= reach; dy++)
                {
                    Point tile = new(origin.X + dx, origin.Y + dy);
                    int distance = Math.Abs(dx) + Math.Abs(dy);
                    if (distance < Math.Max(1, definition.MinRange)
                        || !state.Map.Contains(tile)
                        || tiles.Contains(tile)
                        || (distance >= 2 && !UnitRules.HasLineOfSight(state, placed, tile)))
                    {
                        continue;
                    }

                    tiles.Add(tile);
                }
            }
        }

        return tiles;
    }
}
