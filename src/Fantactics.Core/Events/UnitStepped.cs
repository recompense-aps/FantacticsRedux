using Fantactics.Core.Geometry;

namespace Fantactics.Core.Events;

/// <summary>A unit moved one tile.</summary>
/// <param name="Tick">Movement tick, starting at 1; 0 for moves outside the tick sequence (e.g. taking a clash tile).</param>
/// <param name="UnitId">The unit.</param>
/// <param name="From">Previous tile.</param>
/// <param name="To">New tile.</param>
public sealed record UnitStepped(int Tick, int UnitId, Point From, Point To) : GameEvent;
