using System.Collections.Immutable;
using Fantactics.Core.Geometry;

namespace Fantactics.Core.Engine;

/// <summary>What a seat must place and where.</summary>
/// <param name="UnitIds">Starting units to place.</param>
/// <param name="Tiles">Legal tiles (passable, in the deploy zone).</param>
public sealed record PlacementOptions(ImmutableArray<int> UnitIds, ImmutableArray<Point> Tiles);
