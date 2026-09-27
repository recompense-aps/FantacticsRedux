using Fantactics.Core.Geometry;

namespace Fantactics.Core.Events;

/// <summary>A starting unit was revealed on its placement tile.</summary>
/// <param name="UnitId">The unit.</param>
/// <param name="Owner">Owning seat.</param>
/// <param name="Type">Unit type.</param>
/// <param name="Tile">Placement tile.</param>
public sealed record UnitPlaced(int UnitId, Seat Owner, string Type, Point Tile) : GameEvent;
