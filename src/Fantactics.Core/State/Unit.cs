using System.Collections.Immutable;
using Fantactics.Core.Geometry;

namespace Fantactics.Core.State;

/// <summary>A living unit.</summary>
/// <param name="Id">Deterministic identifier (draft order, then summons and arrivals in order).</param>
/// <param name="Owner">Owning seat.</param>
/// <param name="Type">Unit type identifier in the rules config.</param>
/// <param name="Location">Where the unit is.</param>
/// <param name="Position">Tile, meaningful only when <paramref name="Location"/> is <see cref="UnitLocation.Field"/>.</param>
/// <param name="Hp">Current HP.</param>
/// <param name="Statuses">Active statuses, each with the last turn it lasts through.</param>
/// <param name="AbilityReadyTurn">First turn each used ability is available again.</param>
/// <param name="IsSummoned">Created by an ability; worth no points (RacesAndUnits §2.1).</param>
/// <param name="CannotActOnTurn">A turn on which the unit gets no action (summoned, or arrived outside its zone).</param>
/// <param name="ExtraRaces">
/// Races the unit has on top of its type's race, such as a raised Undead unit (RacesAndUnits §2.4); <c>null</c> for none.
/// </param>
public sealed record Unit(
    int Id,
    Seat Owner,
    string Type,
    UnitLocation Location,
    Point Position,
    int Hp,
    ImmutableSortedDictionary<StatusKind, int> Statuses,
    ImmutableSortedDictionary<string, int> AbilityReadyTurn,
    bool IsSummoned,
    int CannotActOnTurn,
    ImmutableSortedSet<string>? ExtraRaces = null)
{
    /// <summary>Creates a fresh unit with no statuses or cooldowns.</summary>
    public static Unit Create(int id, Seat owner, string type, UnitLocation location, Point position, int hp) =>
        new(
            id,
            owner,
            type,
            location,
            position,
            hp,
            ImmutableSortedDictionary<StatusKind, int>.Empty,
            ImmutableSortedDictionary<string, int>.Empty,
            IsSummoned: false,
            CannotActOnTurn: 0);

    /// <summary>Whether the unit is on the map.</summary>
    public bool IsOnField => Location == UnitLocation.Field;

    /// <summary>Whether the unit currently has <paramref name="status"/>.</summary>
    public bool Has(StatusKind status) => Statuses.ContainsKey(status);
}
