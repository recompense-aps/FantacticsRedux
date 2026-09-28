using System.Collections.Immutable;
using Fantactics.Core.Commands;
using Fantactics.Core.Events;
using Fantactics.Core.Geometry;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Core.Engine;

/// <summary>Resolves both players' hidden orders: arrivals, movement ticks, clashes, then the action order.</summary>
internal static class MovementResolver
{
    /// <summary>Resolves the movement phase once both seats' orders are in.</summary>
    public static GameState Resolve(GameState state, List<GameEvent> events)
    {
        Dictionary<Seat, SubmitMoveOrders> orders = SeatExtensions.All.ToDictionary(
            seat => seat,
            seat => (SubmitMoveOrders)state.PendingOrders[seat]);
        state = state with { PendingOrders = ImmutableSortedDictionary<Seat, ICommand>.Empty };

        HashSet<int> startedOnField = state.FieldUnits.Select(unit => unit.Id).ToHashSet();
        (state, HashSet<int> arrivalWinners) = ResolveArrivals(state, orders, events);

        Dictionary<int, ImmutableArray<Point>> paths = orders.Values
            .SelectMany(o => o.Moves)
            .Where(move => !move.Path.IsEmpty)
            .ToDictionary(move => move.UnitId, move => move.Path);
        MovementSimulation simulation = new(state, paths, events);
        simulation.Run();

        foreach ((int id, Point position) in simulation.Positions)
        {
            state = state.WithUnit(state.Units[id] with { Position = position });
        }

        (state, HashSet<int> winners) = ClashResolver.Resolve(state, simulation.Clashes, events);
        winners.UnionWith(arrivalWinners.Where(state.Units.ContainsKey));

        HashSet<int> moved = simulation.Moved.Concat(winners).Where(state.Units.ContainsKey).ToHashSet();
        ImmutableSortedSet<int> held = startedOnField
            .Where(id => !paths.ContainsKey(id)
                && state.Units.TryGetValue(id, out Unit? unit)
                && !unit.Has(StatusKind.Rooted))
            .ToImmutableSortedSet();
        ImmutableSortedSet<int> braced = held
            .Where(id => IsBraced(state, state.Units[id], moved))
            .ToImmutableSortedSet();

        return InitiativeRules.BeginActionPhase(state, held, braced, winners.ToImmutableSortedSet(), events);
    }

    /// <summary>
    /// A held unit with Braced that an enemy moved next to (or, with <see cref="BracedTrigger.InRange"/>, into its
    /// range) this turn (GameDesign §4.1).
    /// </summary>
    private static bool IsBraced(GameState state, Unit unit, HashSet<int> moved)
    {
        if (!UnitRules.HasTrait(state, unit, TraitIds.Braced))
        {
            return false;
        }

        int maxDistance = state.Rules.BracedTrigger == BracedTrigger.Adjacent ? 1 : state.DefinitionOf(unit).MaxRange;
        return state.FieldUnits.Any(enemy =>
            enemy.Owner != unit.Owner
            && moved.Contains(enemy.Id)
            && enemy.Position.DistanceTo(unit.Position) <= maxDistance);
    }

    /// <summary>
    /// Places reserve arrivals before any unit moves. If both players deploy onto the same tile, the two arrivals
    /// clash on it right away (GameDesign §4.4): both fight as if standing on the tile, and the survivor keeps it.
    /// </summary>
    /// <returns>The new state and the arrival clash winners (who get no action this turn).</returns>
    private static (GameState State, HashSet<int> Winners) ResolveArrivals(
        GameState state,
        Dictionary<Seat, SubmitMoveOrders> orders,
        List<GameEvent> events)
    {
        List<(Seat Seat, DeployOrder Deploy)> deploys = SeatExtensions.All
            .SelectMany(seat => orders[seat].Deploys.Select(deploy => (seat, deploy)))
            .ToList();

        foreach ((Seat seat, DeployOrder deploy) in deploys)
        {
            Unit unit = state.Units[deploy.UnitId];
            int cost = UnitRules.DeployCost(state, unit);
            bool inZone = state.Map.IsInDeployZone(deploy.Tile, seat, state.Rules.DeployColumns);
            PlayerState player = state.Players[seat];
            state = state
                .WithPlayer(player with { Command = player.Command - cost })
                .WithUnit(unit with
                {
                    Location = UnitLocation.Field,
                    Position = deploy.Tile,
                    CannotActOnTurn = inZone ? unit.CannotActOnTurn : state.Turn,
                });
            events.Add(new UnitArrived(unit.Id, seat, unit.Type, deploy.Tile, cost, inZone));
        }

        HashSet<int> winners = [];
        IEnumerable<(Point Tile, int A, int B)> contested = deploys
            .GroupBy(d => d.Deploy.Tile)
            .Where(group => group.Count() == 2)
            .OrderBy(group => group.Key)
            .Select(group => (group.Key, group.First().Deploy.UnitId, group.Last().Deploy.UnitId));
        foreach ((Point tile, int a, int b) in contested)
        {
            events.Add(new ClashMarked(0, a, b, tile));
            (state, int? winner) = ClashResolver.Fight(state, a, b, events);
            if (winner is int winnerId)
            {
                winners.Add(winnerId);
            }
            else
            {
                // Strike cap reached with both alive: neither keeps the tile, and both return to reserve.
                state = new[] { a, b }.Aggregate(state, ReturnToReserve);
            }
        }

        return (state, winners);
    }

    private static GameState ReturnToReserve(GameState state, int unitId)
    {
        Unit unit = state.Units[unitId];
        PlayerState player = state.Players[unit.Owner];
        return state
            .WithPlayer(player with { Command = player.Command + UnitRules.DeployCost(state, unit) })
            .WithUnit(unit with { Location = UnitLocation.Reserve });
    }
}
