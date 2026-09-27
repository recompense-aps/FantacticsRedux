using Fantactics.Core.Geometry;

namespace Fantactics.Core.Maps;

/// <summary>
/// Center-to-center line of sight on the square grid (GameDesign §5), computed with integer math only.
/// </summary>
public static class LineOfSight
{
    /// <summary>Whether the line from <paramref name="from"/> to <paramref name="to"/> is unobstructed.</summary>
    /// <param name="from">Attacker's tile; never blocks.</param>
    /// <param name="to">Target's tile; never blocks.</param>
    /// <param name="blocks">Whether a tile blocks sight.</param>
    /// <remarks>
    /// The line is blocked if it passes through the interior of a blocking tile. A line that passes exactly
    /// through a corner is blocked only if both tiles touching that corner (off the line) block.
    /// </remarks>
    public static bool IsClear(Point from, Point to, Func<Point, bool> blocks)
    {
        int dx = Math.Abs(to.X - from.X);
        int dy = Math.Abs(to.Y - from.Y);
        int stepX = to.X > from.X ? 1 : -1;
        int stepY = to.Y > from.Y ? 1 : -1;
        int x = from.X;
        int y = from.Y;
        // Supercover traversal: error tracks which cell boundary the line crosses next (scaled by 2 to stay integral).
        int error = dx - dy;
        dx *= 2;
        dy *= 2;
        int remaining = dx / 2 + dy / 2;
        while (remaining > 0)
        {
            if (error > 0)
            {
                x += stepX;
                error -= dy;
                remaining--;
            }
            else if (error < 0)
            {
                y += stepY;
                error += dx;
                remaining--;
            }
            else
            {
                if (blocks(new Point(x + stepX, y)) && blocks(new Point(x, y + stepY)))
                {
                    return false;
                }

                x += stepX;
                y += stepY;
                error += dx - dy;
                remaining -= 2;
            }

            Point current = new(x, y);
            if (current != to && blocks(current))
            {
                return false;
            }
        }

        return true;
    }
}
