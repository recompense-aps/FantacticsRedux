using System.Collections.Immutable;
using Fantactics.Core.Commands;
using Fantactics.Core.Events;
using Fantactics.Core.Geometry;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Core.Engine;

/// <summary>Resolves every player's hidden orders: arrivals, movement ticks, clashes, then the action order.</summary>
internal static class MovementResolver
{
    /// <summary>Resolves the movement phase once every live seat's orders are in.</summary>
    public static GameState Resolve(GameState state, List<GameEvent> events)
    {
        Dictionary<Seat, SubmitMoveOrders> orders = state.LiveSeats.ToDictionary(
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
            state.AreEnemies(enemy, unit)
            && moved.Contains(enemy.Id)
            && enemy.Position.DistanceTo(unit.Position) <= maxDistance);
    }

    /// <summary>
    /// Places reserve arrivals before any unit moves. If enemies deploy onto the same tile, the arrivals clash on it
    /// right away (GameDesign §4.4): they fight as if standing on the tile, and the survivor keeps it. With three or
    /// more teams, the survivor takes on the next arrival, highest initiative first. If teammates pick the same tile,
    /// only the lowest seat's unit arrives; the others stay in reserve.
    /// </summary>
    /// <returns>The new state and the arrival clash winners (who get no action this turn).</returns>
    private static (GameState State, HashSet<int> Winners) ResolveArrivals(
        GameState state,
        Dictionary<Seat, SubmitMoveOrders> orders,
        List<GameEvent> events)
    {
        List<(Seat Seat, DeployOrder Deploy)> deploys = orders.Keys
            .Order()
            .SelectMany(seat => orders[seat].Deploys.Select(deploy => (seat, deploy)))
            .GroupBy(d => (d.deploy.Tile, Team: state.TeamOf(d.seat)))
            .Select(group => group.First())
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
        List<Seat> tieOrder = [.. state.TieOrder];
        IEnumerable<(Point Tile, List<int> Units)> contested = deploys
            .GroupBy(d => d.Deploy.Tile)
            .Where(group => group.Count() >= 2)
            .OrderBy(group => group.Key)
            .Select(group => (group.Key, group.Count() == 2
                ? group.Select(d => d.Deploy.UnitId).ToList()
                : group
                    .OrderByDescending(d => state.DefinitionOf(state.Units[d.Deploy.UnitId]).Initiative)
                    .ThenBy(d => tieOrder.IndexOf(d.Seat))
                    .Select(d => d.Deploy.UnitId)
                    .ToList()));
        foreach ((Point tile, List<int> units) in contested)
        {
            int? holder = null;
            bool fought = false;
            foreach (int challenger in units)
            {
                if (holder is not int current)
                {
                    holder = challenger;
                    continue;
                }

                events.Add(new ClashMarked(0, current, challenger, tile));
                (state, holder) = ClashResolver.Fight(state, current, challenger, events);
                fought = true;
                if (holder is null)
                {
                    // Strike cap reached with both alive: neither keeps the tile, and both return to reserve.
                    state = new[] { current, challenger }.Aggregate(state, ReturnToReserve);
                    fought = false;
                }
            }

            if (fought && holder is int winnerId)
            {
                winners.Add(winnerId);
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
