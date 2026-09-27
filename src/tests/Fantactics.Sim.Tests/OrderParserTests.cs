using Fantactics.Core;
using Fantactics.Core.Commands;
using Fantactics.Core.Geometry;
using Fantactics.Core.Scenarios;
using Fantactics.Core.State;
using Fantactics.Sim.Matches;
using Fantactics.Sim.Orders;

namespace Fantactics.Sim.Tests;

/// <summary>The compact order grammar (Simulation §6.1).</summary>
public class OrderParserTests
{
    private readonly GameState _state;
    private readonly UnitHandles _handles;
    private readonly int _ranger;
    private readonly int _archer;
    private readonly int _reserve;

    public OrderParserTests()
    {
        _state = new ScenarioBuilder()
            .WithMap(".......", ".......", ".......", ".......", ".......")
            .AddUnit(Seat.P1, "Ranger", 0, 2, out _ranger)
            .AddUnit(Seat.P1, "Archer", 0, 0, out _archer)
            .AddReserve(Seat.P1, "Scout", out _reserve)
            .AddUnit(Seat.P2, "Grunt", 6, 4, out int grunt)
            .Build();
        _handles = new UnitHandles([_ranger, _archer, _reserve], [grunt]);
    }

    [Fact]
    public void MovesTakeTheCheapestPath()
    {
        SubmitMoveOrders orders = OrderParser.ParseMoveOrders("A>3,2", _state, Seat.P1, _handles);

        MoveOrder move = Assert.Single(orders.Moves);
        Assert.Equal(_ranger, move.UnitId);
        Assert.Equal([new Point(1, 2), new Point(2, 2), new Point(3, 2)], move.Path.ToArray());
    }

    [Fact]
    public void WaypointsPickTheRoute()
    {
        SubmitMoveOrders orders = OrderParser.ParseMoveOrders("A>0,3>1,3", _state, Seat.P1, _handles);

        Assert.Equal([new Point(0, 3), new Point(1, 3)], orders.Moves[0].Path.ToArray());
    }

    [Fact]
    public void HoldsAndDeploysParse()
    {
        SubmitMoveOrders orders = OrderParser.ParseMoveOrders("A=hold B>1,0 C@0,4", _state, Seat.P1, _handles);

        Assert.Equal(_archer, Assert.Single(orders.Moves).UnitId);
        Assert.Equal(new DeployOrder(_reserve, new Point(0, 4)), Assert.Single(orders.Deploys));
    }

    [Theory]
    [InlineData("a>1,1")]
    [InlineData("A>1")]
    [InlineData("A=wait")]
    [InlineData("C>1,1")]
    [InlineData("hello")]
    public void BadOrdersAreRuleViolations(string text)
    {
        SimException ex = Assert.Throws<SimException>(() => OrderParser.ParseMoveOrders(text, _state, Seat.P1, _handles));

        Assert.Equal(ExitCodes.RuleViolation, ex.ExitCode);
    }

    [Fact]
    public void DraftSplitsStartingAndReserve()
    {
        SubmitDraft draft = OrderParser.ParseDraft("Archer Archer Ranger | Scout");

        Assert.Equal(["Archer", "Archer", "Ranger"], draft.Starting.ToArray());
        Assert.Equal(["Scout"], draft.Reserve.ToArray());
    }

    [Fact]
    public void HandlesAreLettersUppercaseForOwnLowercaseForEnemy()
    {
        UnitHandles handles = new(Enumerable.Range(1, 28), [100]);

        Assert.Equal("A", handles.Of(1));
        Assert.Equal("Z", handles.Of(26));
        Assert.Equal("AA", handles.Of(27));
        Assert.Equal("a", handles.Of(100));
        Assert.Equal("#999", handles.Of(999));
        Assert.Equal(27, handles.Resolve("AA"));
    }
}
