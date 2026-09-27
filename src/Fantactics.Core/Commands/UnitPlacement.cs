using Fantactics.Core.Geometry;

namespace Fantactics.Core.Commands;

/// <summary>Where one starting unit is placed.</summary>
/// <param name="UnitId">The unit.</param>
/// <param name="Tile">A tile in the player's deploy zone.</param>
public sealed record UnitPlacement(int UnitId, Point Tile);
