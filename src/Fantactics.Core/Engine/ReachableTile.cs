using System.Collections.Immutable;
using Fantactics.Core.Geometry;

namespace Fantactics.Core.Engine;

/// <summary>A tile a unit can move to this turn.</summary>
/// <param name="Tile">Destination.</param>
/// <param name="Cost">Movement points spent.</param>
/// <param name="Path">Cheapest path, not including the unit's current tile.</param>
public sealed record ReachableTile(Point Tile, int Cost, ImmutableArray<Point> Path);
