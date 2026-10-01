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
    public void RiverfordHasFourPointSymmetricFordObjectives()
    {
        GameMap map = MapLibrary.Load("riverford");

        Assert.Equal(4, map.Objectives.Length);
        Assert.All(map.Objectives, p => Assert.Contains(new Point(19 - p.X, 13 - p.Y), map.Objectives));
        Assert.All(map.Objectives, p => Assert.Equal(Terrain.Plains, map[p]));
    }

    [Fact]
    public void ObjectivesMustBeOnTheMap()
    {
        Assert.Throws<FormatException>(() => MapParser.Parse("bad", ["...", "@objectives 5,5"]));
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

    [Fact]
    public void RiverfordsDeployDirectivesMatchTheDefaultColumns()
    {
        GameMap map = MapLibrary.Load("riverford");
        GameMap columns = map with { DeployZones = null };

        Assert.Equal(2, map.Seats);
        Assert.NotNull(map.DeployZones);
        Assert.All(
            map.AllPoints().SelectMany(p => new[] { (Point: p, Seat: Seat.P1), (Point: p, Seat: Seat.P2) }),
            pair =>
            {
                Assert.Equal(
                    columns.IsInDeployZone(pair.Point, pair.Seat, 3),
                    map.IsInDeployZone(pair.Point, pair.Seat, 3));
                Assert.Equal(
                    columns.IsInOrNextToDeployZone(pair.Point, pair.Seat, 3),
                    map.IsInOrNextToDeployZone(pair.Point, pair.Seat, 3));
            });
    }

    [Fact]
    public void CrossroadsSeatsFourWithZonesOnEveryEdge()
    {
        GameMap map = MapLibrary.Load("crossroads");

        Assert.Equal(4, map.Seats);
        Assert.All(Enum.GetValues<Seat>(), seat => Assert.True(map.HasDeployZone(seat)));
        Assert.All(map.AllPoints(), p =>
        {
            Assert.Equal(map[p], map[new Point(p.Y, p.X)]);
            Assert.Equal(map[p], map[new Point(15 - p.X, p.Y)]);
            Assert.True(Enum.GetValues<Seat>().Count(seat => map.IsInDeployZone(p, seat, 3)) <= 1);
        });
        Assert.True(map.IsInDeployZone(new Point(0, 7), Seat.P1, 3));
        Assert.True(map.IsInDeployZone(new Point(7, 0), Seat.P2, 3));
        Assert.True(map.IsInDeployZone(new Point(15, 7), Seat.P3, 3));
        Assert.True(map.IsInDeployZone(new Point(7, 15), Seat.P4, 3));
        Assert.False(map.IsInDeployZone(new Point(0, 0), Seat.P1, 3));
    }

    [Fact]
    public void DeployDirectivesAddRectanglesPerSeat()
    {
        GameMap map = MapParser.Parse(
            "m",
            ["....", "....", "....", "@seats 3", "@deploy P1 0,0-0,2", "@deploy P3 3,0 3,2", "@deploy P1 1,0"]);

        Assert.Equal(3, map.Seats);
        Assert.True(map.IsInDeployZone(new Point(1, 0), Seat.P1, 3));
        Assert.False(map.IsInDeployZone(new Point(1, 1), Seat.P1, 3));
        Assert.True(map.IsInDeployZone(new Point(3, 2), Seat.P3, 3));
        Assert.False(map.IsInDeployZone(new Point(3, 1), Seat.P3, 3));
        Assert.False(map.HasDeployZone(Seat.P2));
        Assert.False(map.IsInOrNextToDeployZone(new Point(2, 1), Seat.P1, 3));
        Assert.True(map.IsInOrNextToDeployZone(new Point(1, 1), Seat.P1, 3));
    }

    [Theory]
    [InlineData("@seats 5")]
    [InlineData("@seats 1")]
    [InlineData("@seats two")]
    [InlineData("@deploy P1")]
    [InlineData("@deploy P9 0,0-1,1")]
    [InlineData("@deploy P1 0,0-9,9")]
    [InlineData("@deploy P3 0,0-1,1")]
    public void BadSeatAndDeployDirectivesAreRejected(string directive)
    {
        Assert.Throws<FormatException>(() => MapParser.Parse("bad", ["...", "...", directive]));
    }
}
