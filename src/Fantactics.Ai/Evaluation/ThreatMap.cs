using System.Collections.Immutable;
using Fantactics.Core;
using Fantactics.Core.Engine;
using Fantactics.Core.Geometry;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Ai.Evaluation;

/// <summary>
/// Which tiles each enemy unit could strike next turn: every tile in attack range (and line of sight) of every
/// tile it can move to, or of where it stands. Computed once per decision and reused for every option scored.
/// </summary>
public sealed class ThreatMap
{
    private readonly ImmutableSortedDictionary<int, HashSet<Point>> _targets;

    private ThreatMap(ImmutableSortedDictionary<int, HashSet<Point>> targets)
    {
        _targets = targets;
    }

    /// <summary>An empty map, for bots that ignore threats.</summary>
    public static ThreatMap Empty { get; } = new(ImmutableSortedDictionary<int, HashSet<Point>>.Empty);

    /// <summary>Computes the threats of the units on the field that are enemies of <paramref name="seat"/>.</summary>
    public static ThreatMap ForEnemiesOf(GameState state, Seat seat) =>
        new(state.FieldUnits
            .Where(unit => state.AreEnemies(unit.Owner, seat))
            .ToImmutableSortedDictionary(unit => unit.Id, unit => StrikeTiles(state, unit)));

    /// <summary>Whether unit <paramref name="attackerId"/> could strike <paramref name="tile"/> next turn.</summary>
    public bool Threatens(int attackerId, Point tile) =>
        _targets.TryGetValue(attackerId, out HashSet<Point>? tiles) && tiles.Contains(tile);

    private static HashSet<Point> StrikeTiles(GameState state, Unit unit)
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
