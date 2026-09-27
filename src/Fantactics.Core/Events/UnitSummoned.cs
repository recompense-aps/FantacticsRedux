using Fantactics.Core.Geometry;

namespace Fantactics.Core.Events;

/// <summary>An ability created a unit. It can't act this turn and is worth no points.</summary>
/// <param name="UnitId">The new unit.</param>
/// <param name="Owner">Owning seat.</param>
/// <param name="Type">Unit type.</param>
/// <param name="Tile">Where it appeared.</param>
public sealed record UnitSummoned(int UnitId, Seat Owner, string Type, Point Tile) : GameEvent;
