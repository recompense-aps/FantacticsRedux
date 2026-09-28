using System.Collections.Immutable;
using Fantactics.Ai.Evaluation;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Geometry;
using Fantactics.Core.State;

namespace Fantactics.Ai.Planning;

/// <summary>
/// Builds one full set of movement-phase orders (GameDesign §4.1, §4.4) for a given set of weights: each unit in
/// turn takes the best-scoring tile it can reach (or holds), seeing where the units before it chose to go; then
/// reserves deploy onto the best free tiles.
/// </summary>
public static class CandidateGenerator
{
    /// <summary>
    /// Builds orders from <paramref name="options"/>, scoring tiles with <paramref name="evaluator"/>.
    /// </summary>
    public static SubmitMoveOrders Build(PlanningContext context, MoveOptions options, Evaluator evaluator)
    {
        GameState state = context.Belief;
        Dictionary<int, UnitMoveOptions> reach = options.Units.ToDictionary(unit => unit.UnitId);
        Dictionary<int, Point> destinations = state.FieldUnits
            .Where(unit => unit.Owner == context.View.Seat)
            .ToDictionary(unit => unit.Id, unit => unit.Position);
        HashSet<Point> claimed = [];
        List<MoveOrder> moves = [];

        IEnumerable<Unit> byValue = destinations.Keys
            .Select(id => state.Units[id])
            .OrderByDescending(unit => Evaluator.ValueOf(state, unit))
            .ThenBy(unit => unit.Id);
        foreach (Unit unit in byValue)
        {
            List<Point> friends = destinations
                .Where(pair => pair.Key != unit.Id)
                .Select(pair => pair.Value)
                .ToList();
            List<ReachableTile> candidates = reach.TryGetValue(unit.Id, out UnitMoveOptions? unitOptions)
                ? unitOptions.Destinations.Where(tile => !claimed.Contains(tile.Tile)).ToList()
                : [];
            candidates.Insert(0, new ReachableTile(unit.Position, 0, []));

            List<double> scores = candidates
                .Select(candidate => evaluator.ScoreUnitAt(state, unit, candidate.Tile, friends))
                .ToList();
            ReachableTile choice = candidates[context.Mistakes.Choose(scores)];
            destinations[unit.Id] = choice.Tile;
            if (!choice.Path.IsEmpty)
            {
                claimed.Add(choice.Tile);
                moves.Add(new MoveOrder(unit.Id, choice.Path));
            }
        }

        ImmutableArray<DeployOrder> deploys = Deploys(context, options, evaluator, destinations, claimed);
        return new SubmitMoveOrders([.. moves.OrderBy(move => move.UnitId)], deploys);
    }

    /// <summary>
    /// Deploys the most expensive affordable reserves first. Bots with low reserve eagerness bank Command until
    /// they can bring in their most expensive reserve.
    /// </summary>
    private static ImmutableArray<DeployOrder> Deploys(
        PlanningContext context,
        MoveOptions options,
        Evaluator evaluator,
        Dictionary<int, Point> destinations,
        HashSet<Point> claimed)
    {
        GameState state = context.Belief;
        int biggest = state.Units.Values
            .Where(unit => unit.Owner == context.View.Seat && unit.Location == UnitLocation.Reserve)
            .Select(unit => UnitRules.DeployCost(state, unit))
            .DefaultIfEmpty(0)
            .Max();
        bool banking = evaluator.Style.ReserveEagerness < 0.5;

        List<DeployOrder> deploys = [];
        int command = options.Command;
        IEnumerable<DeployOption> byCost = options.Deploys
            .OrderByDescending(option => option.Cost)
            .ThenBy(option => option.UnitId);
        foreach (DeployOption option in byCost)
        {
            List<Point> tiles = option.Tiles.Where(tile => !claimed.Contains(tile)).ToList();
            if (deploys.Count >= options.MaxArrivals
                || option.Cost > command
                || tiles.Count == 0
                || (banking && deploys.Count == 0 && option.Cost < biggest))
            {
                continue;
            }

            Unit unit = state.Units[option.UnitId] with { Location = UnitLocation.Field };
            List<Point> friends = [.. destinations.Values];
            List<double> scores = tiles
                .Select(tile => evaluator.ScoreUnitAt(state, unit with { Position = tile }, tile, friends))
                .ToList();
            Point choice = tiles[context.Mistakes.Choose(scores)];
            deploys.Add(new DeployOrder(option.UnitId, choice));
            claimed.Add(choice);
            destinations[option.UnitId] = choice;
            command -= option.Cost;
        }

        return [.. deploys];
    }
}
