using Fantactics.Core.Engine;
using Fantactics.Core.Geometry;
using Fantactics.Core.Scenarios;
using Fantactics.Core.State;
using static Fantactics.Core.Tests.Play;

namespace Fantactics.Core.Tests;

/// <summary>Where a unit could move and strike next turn, for threat maps and the client's threat overlay.</summary>
public class ThreatRangeTests
{
    [Fact]
    public void AMeleeUnitThreatensEveryTileNextToWhereItCanStandOrMove()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .AddUnit(Seat.P2, "Grunt", 0, 2, out int grunt)
            .AddUnit(Seat.P1, "Grunt", 6, 2)
            .Build();
        Unit unit = state.Units[grunt];

        IReadOnlySet<Point> reach = ThreatRange.Reach(state, unit);
        HashSet<Point> adjacent = reach
            .Append(unit.Position)
            .SelectMany(tile => tile.Neighbors())
            .Where(state.Map.Contains)
            .ToHashSet();

        Assert.True(reach.SetEquals(Pathfinder.Reachable(state, unit).Select(tile => tile.Tile)));
        Assert.True(adjacent.SetEquals(ThreatRange.Strike(state, unit)));
    }

    [Fact]
    public void ARootedArcherThreatensOnlyTilesInRangeOfItsOwnTile()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .AddUnit(Seat.P2, "Archer", 3, 2, out int archer)
            .Modify(unit => unit with { Statuses = unit.Statuses.Add(StatusKind.Rooted, 5) })
            .AddUnit(Seat.P1, "Grunt", 0, 0)
            .Build();
        Unit unit = state.Units[archer];

        IReadOnlySet<Point> strike = ThreatRange.Strike(state, unit);

        Assert.Empty(ThreatRange.Reach(state, unit));
        Assert.All(strike, tile => Assert.InRange(tile.DistanceTo(unit.Position), 2, 3));
        Assert.Contains(new Point(0, 2), strike);
        Assert.DoesNotContain(new Point(4, 2), strike);
    }
}
