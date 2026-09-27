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
        state = ResolveArrivals(state, orders, events);

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

    /// <summary>A held unit with Braced that an enemy moved into range of (GameDesign §4.1).</summary>
    private static bool IsBraced(GameState state, Unit unit, HashSet<int> moved)
    {
        if (!UnitRules.HasTrait(state, unit, TraitIds.Braced))
        {
            return false;
        }

        UnitDefinition definition = state.DefinitionOf(unit);
        return state.FieldUnits.Any(enemy =>
            enemy.Owner != unit.Owner
            && moved.Contains(enemy.Id)
            && enemy.Position.DistanceTo(unit.Position) is int distance
            && distance >= Math.Min(1, definition.MinRange)
            && distance <= definition.MaxRange);
    }

    /// <summary>
    /// Places reserve arrivals before any unit moves. If both players deploy onto the same tile, neither arrives.
    /// </summary>
    private static GameState ResolveArrivals(
        GameState state,
        Dictionary<Seat, SubmitMoveOrders> orders,
        List<GameEvent> events)
    {
        List<(Seat Seat, DeployOrder Deploy)> deploys = SeatExtensions.All
            .SelectMany(seat => orders[seat].Deploys.Select(deploy => (seat, deploy)))
            .ToList();
        HashSet<Point> contested = deploys
            .GroupBy(d => d.Deploy.Tile)
            .Where(group => group.Select(d => d.Seat).Distinct().Count() > 1)
            .Select(group => group.Key)
            .ToHashSet();

        foreach ((Seat seat, DeployOrder deploy) in deploys)
        {
            if (contested.Contains(deploy.Tile))
            {
                events.Add(new ArrivalCancelled(deploy.UnitId, deploy.Tile));
                continue;
            }

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

        return state;
    }
}
