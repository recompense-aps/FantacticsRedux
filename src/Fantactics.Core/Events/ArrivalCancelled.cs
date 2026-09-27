using Fantactics.Core.Geometry;

namespace Fantactics.Core.Events;

/// <summary>Both players deployed onto the same tile, so neither unit arrived and no Command was spent.</summary>
/// <param name="UnitId">A unit whose arrival was cancelled.</param>
/// <param name="Tile">The contested tile.</param>
public sealed record ArrivalCancelled(int UnitId, Point Tile) : GameEvent;
