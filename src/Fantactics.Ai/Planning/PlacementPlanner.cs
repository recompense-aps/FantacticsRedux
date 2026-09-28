using Fantactics.Ai.Profiles;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Geometry;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Ai.Planning;

/// <summary>
/// Places the starting army (GameDesign §4.4) in the style's <see cref="Formation"/>: each unit is drawn toward a
/// goal point (the objectives for a block, its own slot along the front for a line, the top or bottom for flanks),
/// onto defensive terrain, with melee units on the front column and ranged units behind them.
/// </summary>
public static class PlacementPlanner
{
    private const double GoalWeight = 0.5;
    private const double FrontBonus = 0.5;
    private const double CohesionBonus = 0.3;

    /// <summary>Builds a legal placement from <paramref name="options"/>.</summary>
    public static PlaceStartingArmy Plan(PlanningContext context, PlacementOptions options)
    {
        GameState state = context.Belief;
        StyleWeights style = context.Evaluator.Style;
        int center = state.Map.Width / 2;
        int front = options.Tiles
            .Select(tile => Math.Abs(tile.X - center))
            .DefaultIfEmpty(0)
            .Min();
        int frontX = options.Tiles.FirstOrDefault(tile => Math.Abs(tile.X - center) == front).X;

        List<Unit> meleeFirst = options.UnitIds
            .Select(id => state.Units[id])
            .OrderBy(unit => state.DefinitionOf(unit).IsRanged)
            .ThenByDescending(unit => state.DefinitionOf(unit).Cost)
            .ThenBy(unit => unit.Id)
            .ToList();
        List<UnitPlacement> placements = [];
        HashSet<Point> used = [];
        foreach ((Unit unit, int index) in meleeFirst.Select((unit, index) => (unit, index)))
        {
            UnitDefinition definition = state.DefinitionOf(unit);
            IReadOnlyList<Point> goals = Goals(state, style.Formation, frontX, index, meleeFirst.Count);
            double cohesion = style.Formation == Formation.Line ? 0 : CohesionBonus;
            List<Point> free = options.Tiles.Where(tile => !used.Contains(tile)).ToList();
            List<double> scores = free
                .Select(tile =>
                {
                    bool onFront = Math.Abs(tile.X - center) == front;
                    return -GoalWeight * goals.Min(goal => goal.DistanceTo(tile))
                        + style.Terrain * 2 * UnitRules.TerrainDefense(state, tile)
                        + (onFront ? (definition.IsRanged ? -FrontBonus : FrontBonus) : 0)
                        + cohesion * used.Count(other => other.IsAdjacentTo(tile));
                })
                .ToList();
            Point choice = free[context.Mistakes.Choose(scores)];
            used.Add(choice);
            placements.Add(new UnitPlacement(unit.Id, choice));
        }

        return new PlaceStartingArmy([.. placements.OrderBy(placement => placement.UnitId)]);
    }

    /// <summary>Where the <paramref name="index"/>-th of <paramref name="count"/> units wants to stand.</summary>
    private static IReadOnlyList<Point> Goals(GameState state, Formation formation, int frontX, int index, int count)
    {
        int height = state.Map.Height;
        return formation switch
        {
            Formation.Line => [new Point(frontX, (2 * index + 1) * height / (2 * count))],
            Formation.Flanks => [new Point(frontX, index % 2 == 0 ? height / 5 : height - 1 - height / 5)],
            _ when !state.Map.Objectives.IsEmpty => [.. state.Map.Objectives],
            _ => [new Point(state.Map.Width / 2, height / 2)],
        };
    }
}
