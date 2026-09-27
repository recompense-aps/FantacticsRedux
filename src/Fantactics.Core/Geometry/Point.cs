namespace Fantactics.Core.Geometry;

/// <summary>A tile coordinate. The origin is the top-left tile; x grows to the right and y grows down.</summary>
/// <param name="X">Column.</param>
/// <param name="Y">Row.</param>
public readonly record struct Point(int X, int Y) : IComparable<Point>
{
    /// <summary>Manhattan distance to <paramref name="other"/> (GameDesign §5).</summary>
    public int DistanceTo(Point other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

    /// <summary>Whether <paramref name="other"/> is one of the 4 orthogonal neighbors.</summary>
    public bool IsAdjacentTo(Point other) => DistanceTo(other) == 1;

    /// <summary>The 4 orthogonal neighbors in a fixed order: up, right, down, left.</summary>
    public IEnumerable<Point> Neighbors() => [new(X, Y - 1), new(X + 1, Y), new(X, Y + 1), new(X - 1, Y)];

    /// <summary>Orders points row by row, then by column.</summary>
    public int CompareTo(Point other) => Y != other.Y ? Y.CompareTo(other.Y) : X.CompareTo(other.X);

    /// <inheritdoc />
    public override string ToString() => $"({X},{Y})";
}
