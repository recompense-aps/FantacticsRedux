using System.Collections.Immutable;
using Fantactics.Core.Geometry;
using Fantactics.Core.Maps;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Core.Engine;

/// <summary>Derived unit values: traits, movement, terrain, and army value.</summary>
public static class UnitRules
{
    /// <summary>The value of a trait on <paramref name="unit"/> (unit and race traits), or 0 if it doesn't have it.</summary>
    public static int Trait(GameState state, Unit unit, string trait) =>
        TraitsOf(state, unit).GetValueOrDefault(trait);

    /// <summary>Whether <paramref name="unit"/> has <paramref name="trait"/>.</summary>
    public static bool HasTrait(GameState state, Unit unit, string trait) =>
        TraitsOf(state, unit).ContainsKey(trait);

    /// <summary>Every race <paramref name="unit"/> belongs to: its type's race, then any extra races.</summary>
    public static IEnumerable<string> Races(GameState state, Unit unit) =>
        (unit.ExtraRaces ?? []).Prepend(state.DefinitionOf(unit).Race).Distinct();

    /// <summary>Whether <paramref name="a"/> and <paramref name="b"/> have a race in common (RacesAndUnits §2.4).</summary>
    public static bool SharesRace(GameState state, Unit a, Unit b) => Races(state, a).Intersect(Races(state, b)).Any();

    /// <summary>Whether <paramref name="unit"/>'s type carries the class <paramref name="classId"/>.</summary>
    public static bool HasClass(GameState state, Unit unit, string classId) =>
        state.DefinitionOf(unit).Classes?.Contains(classId) == true;

    /// <summary>Movement points this turn after Slowed (−2, minimum 1) and Rooted (0).</summary>
    public static int MovementPoints(GameState state, Unit unit)
    {
        if (unit.Has(StatusKind.Rooted))
        {
            return 0;
        }

        int movement = state.DefinitionOf(unit).Movement;
        return unit.Has(StatusKind.Slowed) ? Math.Max(1, movement - 2) : movement;
    }

    /// <summary>
    /// Movement cost for <paramref name="unit"/> to step from <paramref name="from"/> onto <paramref name="to"/>,
    /// or <c>null</c> if <paramref name="to"/> is off the map or impassable.
    /// </summary>
    public static int? StepCost(GameState state, Unit unit, Point from, Point to)
    {
        if (!state.Map.Contains(to))
        {
            return null;
        }

        Terrain terrain = state.Map[to];
        int? baseCost = state.Rules.Terrain[terrain].MoveCost;
        if (baseCost is null)
        {
            return null;
        }

        if (terrain == Terrain.Forest
            && state.Map.Contains(from)
            && state.Map[from] == Terrain.Forest
            && HasTrait(state, unit, TraitIds.ForestStride))
        {
            return 0;
        }

        return terrain switch
        {
            Terrain.Forest when HasTrait(state, unit, TraitIds.Forestwalk) => 1,
            Terrain.Mountains when HasTrait(state, unit, TraitIds.MountainBorn) => 2,
            _ => baseCost,
        };
    }

    /// <summary>Whether units can stand on <paramref name="tile"/>.</summary>
    public static bool IsPassable(GameState state, Point tile) =>
        state.Map.Contains(tile) && state.Rules.Terrain[state.Map[tile]].IsPassable;

    /// <summary>Terrain Defense of <paramref name="tile"/>.</summary>
    public static int TerrainDefense(GameState state, Point tile) => state.Rules.Terrain[state.Map[tile]].Defense;

    /// <summary>Whether <paramref name="tile"/> blocks line of sight for <paramref name="viewer"/>.</summary>
    public static bool BlocksSightFor(GameState state, Unit viewer, Point tile)
    {
        if (!state.Map.Contains(tile))
        {
            return false;
        }

        Terrain terrain = state.Map[tile];
        if (terrain == Terrain.Forest && HasTrait(state, viewer, TraitIds.CanopySight))
        {
            return false;
        }

        return state.Rules.Terrain[terrain].BlocksSight;
    }

    /// <summary>Whether <paramref name="viewer"/> has line of sight to <paramref name="target"/>.</summary>
    public static bool HasLineOfSight(GameState state, Unit viewer, Point target) =>
        LineOfSight.IsClear(viewer.Position, target, tile => BlocksSightFor(state, viewer, tile));

    /// <summary>Command needed to deploy <paramref name="unit"/> from reserve (GameDesign §4.4).</summary>
    public static int DeployCost(GameState state, Unit unit)
    {
        int cost = state.DefinitionOf(unit).Cost;
        return HasTrait(state, unit, TraitIds.OutOfTheCaves) && cost >= 3 ? cost - 1 : cost;
    }

    /// <summary>Army value (GameDesign §4.5): Cost of living, non-summoned units on the field or in reserve.</summary>
    public static int ArmyValue(GameState state, Seat seat) =>
        state.Units.Values
            .Where(unit => unit.Owner == seat && !unit.IsSummoned)
            .Sum(unit => state.DefinitionOf(unit).Cost);

    /// <summary>Cost of the seat's non-summoned units on the field.</summary>
    public static int FieldValue(GameState state, Seat seat) =>
        state.FieldUnits
            .Where(unit => unit.Owner == seat && !unit.IsSummoned)
            .Sum(unit => state.DefinitionOf(unit).Cost);

    /// <summary>Cost of the seat's undeployed reserve.</summary>
    public static int ReserveValue(GameState state, Seat seat) =>
        state.Units.Values
            .Where(unit => unit.Owner == seat && unit.Location == UnitLocation.Reserve)
            .Sum(unit => state.DefinitionOf(unit).Cost);

    /// <summary>Enemy units on the field orthogonally adjacent to <paramref name="tile"/>.</summary>
    public static IEnumerable<Unit> AdjacentEnemies(GameState state, Seat seat, Point tile) =>
        state.FieldUnits.Where(unit => unit.Owner != seat && unit.Position.IsAdjacentTo(tile));

    private static ImmutableSortedDictionary<string, int> TraitsOf(GameState state, Unit unit) =>
        unit.ExtraRaces is null
            ? state.Rules.TraitsOf(unit.Type)
            : state.Rules.TraitsOf(unit.Type, unit.ExtraRaces);
}
