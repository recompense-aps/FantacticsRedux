using Fantactics.Core.Geometry;
using Fantactics.Core.Maps;

namespace Fantactics.Core.Tests;

public class LineOfSightTests
{
    [Fact]
    public void OpenGroundIsClear()
    {
        Assert.True(LineOfSight.IsClear(new Point(0, 0), new Point(3, 2), _ => false));
    }

    [Fact]
    public void ABlockingTileInBetweenBlocks()
    {
        Assert.False(LineOfSight.IsClear(new Point(0, 0), new Point(3, 0), p => p == new Point(1, 0)));
    }

    [Fact]
    public void EndpointsNeverBlock()
    {
        Point from = new(0, 0);
        Point to = new(2, 0);

        Assert.True(LineOfSight.IsClear(from, to, p => p == from || p == to));
    }

    [Fact]
    public void ExactCornerIsBlockedOnlyIfBothTouchingTilesBlock()
    {
        Point from = new(0, 0);
        Point to = new(2, 2);

        Assert.True(LineOfSight.IsClear(from, to, p => p == new Point(1, 0)));
        Assert.False(LineOfSight.IsClear(from, to, p => p == new Point(1, 0) || p == new Point(0, 1)));
    }

    [Fact]
    public void LineThroughTileInteriorIsBlocked()
    {
        // (0,0) to (2,1) passes through the interiors of (1,0) and (1,1).
        Assert.False(LineOfSight.IsClear(new Point(0, 0), new Point(2, 1), p => p == new Point(1, 1)));
        Assert.False(LineOfSight.IsClear(new Point(0, 0), new Point(2, 1), p => p == new Point(1, 0)));
    }
}
