using System.Collections.Immutable;
using Fantactics.Core.Geometry;

namespace Fantactics.Core.Commands;

/// <summary>A unit's move for the turn, as an exact path (GameDesign §4.1). Units without an order hold.</summary>
/// <param name="UnitId">The moving unit.</param>
/// <param name="Path">Tiles to step onto in order, not including the unit's current tile.</param>
public sealed record MoveOrder(int UnitId, ImmutableArray<Point> Path);
