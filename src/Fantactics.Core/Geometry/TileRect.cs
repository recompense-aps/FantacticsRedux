namespace Fantactics.Core.Geometry;

/// <summary>A rectangle of tiles, both corners included.</summary>
/// <param name="Min">The top-left tile.</param>
/// <param name="Max">The bottom-right tile.</param>
public readonly record struct TileRect(Point Min, Point Max)
{
    /// <summary>Whether <paramref name="point"/> lies inside the rectangle.</summary>
    public bool Contains(Point point) =>
        point.X >= Min.X && point.X <= Max.X && point.Y >= Min.Y && point.Y <= Max.Y;

    /// <summary>A rectangle spanning two opposite corners given in any order.</summary>
    public static TileRect Spanning(Point a, Point b) =>
        new(new Point(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)), new Point(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)));

    /// <inheritdoc />
    public override string ToString() => $"{Min.X},{Min.Y}-{Max.X},{Max.Y}";
}
