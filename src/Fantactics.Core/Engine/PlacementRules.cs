using System.Collections.Immutable;
using Fantactics.Core.Commands;
using Fantactics.Core.Events;
using Fantactics.Core.Geometry;
using Fantactics.Core.State;

namespace Fantactics.Core.Engine;

/// <summary>Hidden, simultaneous placement of the starting armies (GameDesign §4.4).</summary>
internal static class PlacementRules
{
    /// <summary>Throws a <see cref="RuleViolationException"/> unless the placement is legal for the seat.</summary>
    public static void Validate(GameState state, Seat seat, PlaceStartingArmy placement)
    {
        HashSet<int> unplaced = state.Units.Values
            .Where(unit => unit.Owner == seat && unit.Location == UnitLocation.Unplaced)
            .Select(unit => unit.Id)
            .ToHashSet();
        List<int> placedIds = placement.Placements.Select(p => p.UnitId).ToList();
        RuleViolationException.ThrowUnless(
            placedIds.Count == unplaced.Count && unplaced.SetEquals(placedIds),
            "placement-incomplete",
            "Place every starting unit exactly once.");

        RuleViolationException.ThrowUnless(
            placement.Placements.Select(p => p.Tile).Distinct().Count() == placement.Placements.Length,
            "tile-taken",
            "Two units can't share a tile.");

        foreach (UnitPlacement p in placement.Placements)
        {
            RuleViolationException.ThrowUnless(
                state.Map.IsInDeployZone(p.Tile, seat, state.Rules.DeployColumns)
                    && UnitRules.IsPassable(state, p.Tile),
                "outside-deploy-zone",
                $"{p.Tile} is not a passable tile in your deploy zone.");
        }
    }

    /// <summary>Reveals every placement, sets turn-1 tie priority, and starts turn 1.</summary>
    public static GameState Resolve(GameState state, List<GameEvent> events)
    {
        foreach (Seat seat in state.Seats.ToList())
        {
            var placement = (PlaceStartingArmy)state.PendingOrders[seat];
            foreach (UnitPlacement p in placement.Placements.OrderBy(p => p.UnitId))
            {
                Unit unit = state.Units[p.UnitId] with { Location = UnitLocation.Field, Position = p.Tile };
                state = state.WithUnit(unit);
                events.Add(new UnitPlaced(unit.Id, seat, unit.Type, p.Tile));
            }
        }

        state = state with { PendingOrders = ImmutableSortedDictionary<Seat, ICommand>.Empty };
        (Seat priority, ulong rng) = FirstTiePriority(state);
        return TurnRules.StartTurn(state with { RngState = rng }, 1, priority, events);
    }

    /// <summary>
    /// Deathmatch: the player with the lowest value on the field gets turn-1 tie priority; a seeded draw breaks equal
    /// values (GameDesign §4.1). With two players, that draw is a coin flip.
    /// </summary>
    private static (Seat Priority, ulong RngState) FirstTiePriority(GameState state)
    {
        Dictionary<Seat, int> values = state.Seats.ToDictionary(
            seat => seat,
            seat => UnitRules.FieldValue(state, seat));
        int lowest = values.Values.Min();
        List<Seat> tied = values.Keys.Where(seat => values[seat] == lowest).ToList();
        if (tied.Count == 1)
        {
            return (tied[0], state.RngState);
        }

        (int pick, ulong next) = Rng.Next(state.RngState, tied.Count);
        return (tied[pick], next);
    }
}
