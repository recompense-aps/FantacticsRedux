using Fantactics.Core.Geometry;
using Fantactics.Core.Maps;

namespace Fantactics.Client.Logic.Playback;

/// <summary>A tile's terrain changes.</summary>
/// <param name="Tile">The tile.</param>
/// <param name="Terrain">Its new terrain.</param>
public sealed record TileStep(Point Tile, Terrain Terrain) : Step;
