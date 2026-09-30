using System.Collections.Immutable;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Geometry;
using Fantactics.Core.Players;

namespace Fantactics.Ai;

/// <summary>
/// Picks random legal options with a seeded RNG, for fuzzing (Simulation §7). Slightly biased toward attacking and
/// moving so matches end by Rout often enough to exercise the whole rule set.
/// </summary>
/// <param name="seed">RNG seed; the same seed and inputs give the same choices.</param>
public sealed class RandomAgent(int seed) : IPlayerAgent
{
    private const int DraftAttempts = 40;

    private readonly Random _random = new(seed);

    /// <inheritdoc />
    public ICommand Decide(PlayerView view, Decision decision, LegalActions legal) => decision switch
    {
        DraftArmyDecision => Draft(legal.Draft ?? throw MissingOptions(decision)),
        PlaceStartingArmyDecision => Place(legal.Placement ?? throw MissingOptions(decision)),
        SubmitMoveOrdersDecision => Move(view, legal.Moves ?? throw MissingOptions(decision)),
        ChooseUnitActionDecision => Act(legal.Actions),
        _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unknown decision."),
    };

    private static InvalidOperationException MissingOptions(Decision decision) =>
        new($"No legal options supplied for {decision}.");

    private SubmitDraft Draft(DraftOptions options)
    {
        List<string> starting = [];
        List<string> reserve = [];
        HashSet<string> uniquesTaken = [];
        int startingCost = 0;
        int totalCost = 0;
        for (int attempt = 0; attempt < DraftAttempts; attempt++)
        {
            DraftUnitOption pick = options.Units[_random.Next(options.Units.Length)];
            if ((pick.Unique && uniquesTaken.Contains(pick.Type)) || totalCost + pick.Cost > options.Budget)
            {
                continue;
            }

            bool toStarting = startingCost + pick.Cost <= options.StartingCap && (starting.Count == 0 || _random.Next(4) > 0);
            (toStarting ? starting : reserve).Add(pick.Type);
            startingCost += toStarting ? pick.Cost : 0;
            totalCost += pick.Cost;
            if (pick.Unique)
            {
                uniquesTaken.Add(pick.Type);
            }
        }

        return new SubmitDraft([.. starting], [.. reserve]);
    }

    private PlaceStartingArmy Place(PlacementOptions options)
    {
        List<Point> tiles = options.Tiles.OrderBy(_ => _random.Next()).ToList();
        return new PlaceStartingArmy(options.UnitIds
            .Select((id, index) => new UnitPlacement(id, tiles[index]))
            .ToImmutableArray());
    }

    private SubmitMoveOrders Move(PlayerView view, MoveOptions options)
    {
        List<Point> enemies = view.Units
            .Where(unit => view.AreEnemies(unit.Owner, view.Seat) && unit.IsOnField)
            .Select(unit => unit.Position)
            .ToList();
        ImmutableArray<MoveOrder> moves = options.Units
            .Where(unit => !unit.Destinations.IsEmpty && _random.Next(10) < 7)
            .Select(unit => new MoveOrder(unit.UnitId, PickDestination(unit, enemies).Path))
            .ToImmutableArray();

        List<DeployOrder> deploys = [];
        HashSet<Point> usedTiles = [];
        int command = options.Command;
        foreach (DeployOption option in options.Deploys.OrderBy(_ => _random.Next()))
        {
            List<Point> free = option.Tiles.Where(tile => !usedTiles.Contains(tile)).ToList();
            if (deploys.Count >= options.MaxArrivals || option.Cost > command || free.Count == 0)
            {
                continue;
            }

            Point tile = free[_random.Next(free.Count)];
            deploys.Add(new DeployOrder(option.UnitId, tile));
            usedTiles.Add(tile);
            command -= option.Cost;
        }

        return new SubmitMoveOrders(moves, [.. deploys]);
    }

    /// <summary>Half the time, the destination closest to an enemy; otherwise any destination.</summary>
    private ReachableTile PickDestination(UnitMoveOptions unit, List<Point> enemies)
    {
        if (enemies.Count == 0 || _random.Next(2) == 0)
        {
            return unit.Destinations[_random.Next(unit.Destinations.Length)];
        }

        return unit.Destinations
            .OrderBy(destination => enemies.Min(enemy => enemy.DistanceTo(destination.Tile)))
            .ThenBy(destination => destination.Tile)
            .First();
    }

    private ICommand Act(ImmutableArray<ActionOption> options)
    {
        List<ActionOption> active = options.Where(option => option.Command is Attack or UseAbility).ToList();
        List<ActionOption> pool = active.Count > 0 && _random.Next(4) > 0 ? active : [.. options];
        return pool[_random.Next(pool.Count)].Command;
    }
}
