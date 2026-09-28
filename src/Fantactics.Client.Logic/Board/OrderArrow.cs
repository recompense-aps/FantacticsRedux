using System.Collections.Immutable;
using Fantactics.Core.Geometry;

namespace Fantactics.Client.Logic.Board;

/// <summary>A move or deploy order drawn on the board.</summary>
/// <param name="UnitId">The unit.</param>
/// <param name="Points">Tiles from where the unit starts to where it ends; a single tile for an arrival.</param>
/// <param name="Deploy">Whether it is a reserve unit arriving.</param>
public sealed record OrderArrow(int UnitId, ImmutableArray<Point> Points, bool Deploy);
