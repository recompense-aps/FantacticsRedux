using Fantactics.Core.Geometry;
using Fantactics.Core.Maps;

namespace Fantactics.Core.Events;

/// <summary>A tile's terrain changed.</summary>
/// <param name="Tile">The tile.</param>
/// <param name="Terrain">New terrain.</param>
public sealed record TileChanged(Point Tile, Terrain Terrain) : GameEvent;
