using Fantactics.Core.Geometry;
using Fantactics.Core.Maps;

namespace Fantactics.Core.Tests;

public class MapTests
{
    [Fact]
    public void RiverfordIsTwentyByFourteenAndPointSymmetric()
    {
        GameMap map = MapLibrary.Load("riverford");

        Assert.Equal(20, map.Width);
        Assert.Equal(14, map.Height);
        Assert.All(map.AllPoints(), p => Assert.Equal(map[p], map[new Point(19 - p.X, 13 - p.Y)]));
    }

    [Fact]
    public void RaggedRowsAreRejected()
    {
        Assert.Throws<FormatException>(() => MapParser.Parse("bad", ["...", ".."]));
    }

    [Fact]
    public void UnknownCharactersAreRejected()
    {
        Assert.Throws<FormatException>(() => MapParser.Parse("bad", ["..X"]));
    }

    [Fact]
    public void RowsRoundTrip()
    {
        string[] rows = [".%^", "~#=", "+.."];

        Assert.Equal(rows, MapParser.Parse("m", rows).ToRows());
    }

    [Fact]
    public void DeployZonesAreTheBackColumns()
    {
        GameMap map = MapLibrary.Load("riverford");

        Assert.True(map.IsInDeployZone(new Point(2, 5), Seat.P1, 3));
        Assert.False(map.IsInDeployZone(new Point(3, 5), Seat.P1, 3));
        Assert.True(map.IsInDeployZone(new Point(17, 5), Seat.P2, 3));
        Assert.False(map.IsInDeployZone(new Point(16, 5), Seat.P2, 3));
    }
}
