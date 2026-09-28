using Fantactics.Core.Geometry;
using Godot;

namespace Fantactics.Client.Common;

/// <summary>The only place Core's <see cref="Point"/> meets Godot's vectors, and where the tile size lives.</summary>
public static class GodotConversions
{
    /// <summary>Tile size in pixels (GameDesign §5).</summary>
    public const int TileSize = 32;

    /// <summary>The Godot cell for a Core point.</summary>
    public static Vector2I ToCell(this Point point) => new(point.X, point.Y);

    /// <summary>The Core point for a Godot cell.</summary>
    public static Point ToPoint(this Vector2I cell) => new(cell.X, cell.Y);

    /// <summary>The center of a tile, in board pixels.</summary>
    public static Vector2 TileCenter(this Point point) =>
        new((point.X + 0.5f) * TileSize, (point.Y + 0.5f) * TileSize);

    /// <summary>The tile under a board-local position.</summary>
    public static Point TileAt(Vector2 local) =>
        new(Mathf.FloorToInt(local.X / TileSize), Mathf.FloorToInt(local.Y / TileSize));
}
