using Fantactics.Core.Geometry;

namespace Fantactics.Core.Events;

/// <summary>A unit stopped before finishing its path.</summary>
/// <param name="Tick">Movement tick.</param>
/// <param name="UnitId">The unit.</param>
/// <param name="Tile">Where it stopped.</param>
/// <param name="Reason">Why it stopped.</param>
public sealed record UnitStopped(int Tick, int UnitId, Point Tile, StopReason Reason) : GameEvent;
