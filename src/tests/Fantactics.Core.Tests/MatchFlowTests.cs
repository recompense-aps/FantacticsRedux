using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Geometry;
using Fantactics.Core.Maps;
using Fantactics.Core.Rules;
using Fantactics.Core.Scenarios;
using Fantactics.Core.State;
using static Fantactics.Core.Tests.Play;

namespace Fantactics.Core.Tests;

/// <summary>Draft, placement, Command income, and end conditions (GameDesign §4.4, §4.5).</summary>
public class MatchFlowTests
{
    private static readonly SubmitDraft _elfDraft = new(["Archer", "Archer", "Ranger"], ["Scout"]);
    private static readonly SubmitDraft _goblinDraft = new(["Grunt", "Grunt", "Grunt"], ["Tank"]);

    [Fact]
    public void DraftsStayHiddenUntilBothAreIn()
    {
        GameState state = NewMatch();

        GameState afterP1 = Apply(state, Seat.P1, _elfDraft).State;

        Assert.Equal(Phase.Draft, afterP1.Phase);
        Assert.Null(PlayerView.Project(afterP1, Seat.P2).MyPendingOrders);
        Assert.NotNull(PlayerView.Project(afterP1, Seat.P1).MyPendingOrders);
        Assert.Equal([Seat.P2], GameEngine.PendingDecisions(afterP1).Select(d => d.Seat));
    }

    [Fact]
    public void BothDraftsCreateUnitsInDraftOrder()
    {
        GameState state = Drafted();

        Assert.Equal(Phase.Placement, state.Phase);
        Assert.Equal(["Archer", "Archer", "Ranger", "Scout", "Grunt", "Grunt", "Grunt", "Tank"],
            state.Units.Values.Select(u => u.Type));
        Assert.Equal(UnitLocation.Reserve, state.Units[4].Location);
        Assert.DoesNotContain(PlayerView.Project(state, Seat.P2).Units, u => u.Owner == Seat.P1);
    }

    [Theory]
    [InlineData(new[] { "Druid", "Druid" }, new string[0], "duplicate-unique")]
    [InlineData(new[] { "Dragon" }, new string[0], "unknown-unit")]
    [InlineData(new[] { "Ranger", "Ranger", "Ranger", "Ranger", "Ranger", "Ranger" }, new string[0], "over-starting-cap")]
    [InlineData(new[] { "Ranger" }, new[] { "Ranger", "Ranger", "Ranger", "Ranger", "Ranger", "Ranger" }, "over-budget")]
    [InlineData(new string[0], new[] { "Ranger" }, "empty-starting-army")]
    public void IllegalDraftsAreRejected(string[] starting, string[] reserve, string code)
    {
        Rejects(NewMatch(), Seat.P1, new SubmitDraft([.. starting], [.. reserve]), code);
    }

    [Fact]
    public void PlacementOutsideTheDeployZoneIsRejected()
    {
        GameState state = Drafted();

        PlaceStartingArmy placement = new([
            new UnitPlacement(1, new Point(0, 0)),
            new UnitPlacement(2, new Point(0, 1)),
            new UnitPlacement(3, new Point(5, 5)),
        ]);

        Rejects(state, Seat.P1, placement, "outside-deploy-zone");
    }

    [Fact]
    public void LowerValueOnTheFieldGetsTurnOnePriorityAndIncomeStartsOnTurnTwo()
    {
        GameState state = Placed();

        Assert.Equal(1, state.Turn);
        Assert.Equal(Phase.Movement, state.Phase);
        Assert.Equal(Seat.P2, state.TiePriority);
        Assert.All(state.Players.Values, p => Assert.Equal(0, p.Command));

        (GameState moved, _) = Moves(state, SubmitMoveOrders.HoldAll, SubmitMoveOrders.HoldAll);
        (GameState turnTwo, _) = WaitOutTurn(moved);

        Assert.Equal(2, turnTwo.Turn);
        Assert.All(turnTwo.Players.Values, p => Assert.Equal(2, p.Command));
    }

    [Fact]
    public void APlayerBelowTheRoutThresholdLosesAtTheEndOfTheTurn()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Scout", 0, 0)
            .AddUnit(Seat.P2, "Tank", 6, 4)
            .AddUnit(Seat.P2, "Tank", 6, 3)
            .AddUnit(Seat.P2, "Tank", 6, 2)
            .Build();

        (GameState moved, _) = Moves(state, SubmitMoveOrders.HoldAll, SubmitMoveOrders.HoldAll);
        (GameState over, _) = WaitOutTurn(moved);

        Assert.Equal(Phase.Over, over.Phase);
        Assert.Equal<Seat>([Seat.P2], over.Outcome!.Winners);
        Assert.Equal(EndReason.Rout, over.Outcome.Reason);
        Assert.Empty(GameEngine.PendingDecisions(over));
    }

    [Fact]
    public void TheTurnLimitIsDecidedByDestroyedValue()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .WithTurn(RulesConfig.Default.TurnLimit)
            .AddUnit(Seat.P1, "Druid", 0, 0)
            .AddUnit(Seat.P1, "Ranger", 0, 1)
            .AddUnit(Seat.P2, "WarLord", 6, 4)
            .AddUnit(Seat.P2, "Shaman", 6, 3)
            .Build();
        state = state.WithPlayer(state.Players[Seat.P1] with { DestroyedValue = 3 });

        (GameState moved, _) = Moves(state, SubmitMoveOrders.HoldAll, SubmitMoveOrders.HoldAll);
        (GameState over, _) = WaitOutTurn(moved);

        Assert.Equal<Seat>([Seat.P1], over.Outcome!.Winners);
        Assert.Equal(EndReason.TurnLimit, over.Outcome.Reason);
    }

    [Fact]
    public void HoldingMoreObjectiveTilesScoresAtTheEndOfTheTurn()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .WithObjectives((3, 1), (3, 3))
            .AddUnit(Seat.P1, "Druid", 3, 1)
            .AddUnit(Seat.P2, "WarLord", 6, 4)
            .WithBallast()
            .Build();

        (GameState moved, _) = Moves(state, SubmitMoveOrders.HoldAll, SubmitMoveOrders.HoldAll);
        (GameState next, List<Events.GameEvent> events) = WaitOutTurn(moved);

        Assert.Equal(2, next.Players[Seat.P1].ObjectivePoints);
        Assert.Equal(0, next.Players[Seat.P2].ObjectivePoints);
        Assert.Contains(events, e => e is Events.ObjectiveScored { Seat: Seat.P1, Held: 1, EnemyHeld: 0 });
    }

    [Fact]
    public void HoldingEquallyManyObjectiveTilesScoresNothing()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .WithObjectives((3, 1), (3, 3))
            .AddUnit(Seat.P1, "Druid", 3, 1)
            .AddUnit(Seat.P2, "WarLord", 3, 3)
            .WithBallast()
            .Build();

        (GameState moved, _) = Moves(state, SubmitMoveOrders.HoldAll, SubmitMoveOrders.HoldAll);
        (GameState next, _) = WaitOutTurn(moved);

        Assert.All(next.Players.Values, player => Assert.Equal(0, player.ObjectivePoints));
    }

    [Fact]
    public void ObjectivePointsCountAtTheTurnLimit()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .WithObjectives((3, 1))
            .WithTurn(RulesConfig.Default.TurnLimit)
            .AddUnit(Seat.P1, "Druid", 3, 1)
            .AddUnit(Seat.P1, "Ranger", 0, 1)
            .AddUnit(Seat.P2, "WarLord", 6, 4)
            .AddUnit(Seat.P2, "Shaman", 6, 3)
            .Build();
        state = state
            .WithPlayer(state.Players[Seat.P1] with { ObjectivePoints = 2 })
            .WithPlayer(state.Players[Seat.P2] with { DestroyedValue = 3 });

        (GameState moved, _) = Moves(state, SubmitMoveOrders.HoldAll, SubmitMoveOrders.HoldAll);
        (GameState over, _) = WaitOutTurn(moved);

        // P1: 0 destroyed + 2 + 2 objective = 4 beats P2's 3 destroyed.
        Assert.Equal<Seat>([Seat.P1], over.Outcome!.Winners);
        Assert.Equal(EndReason.TurnLimit, over.Outcome.Reason);
    }

    private static GameState NewMatch() =>
        GameEngine.NewMatch(RulesConfig.Default, MapLibrary.Load("riverford"), seed: 42);

    private static GameState Drafted()
    {
        GameState state = Apply(NewMatch(), Seat.P1, _elfDraft).State;
        return Apply(state, Seat.P2, _goblinDraft).State;
    }

    private static GameState Placed()
    {
        GameState state = Drafted();
        state = Apply(state, Seat.P1, new PlaceStartingArmy([
            new UnitPlacement(1, new Point(0, 5)),
            new UnitPlacement(2, new Point(0, 6)),
            new UnitPlacement(3, new Point(0, 7)),
        ])).State;
        return Apply(state, Seat.P2, new PlaceStartingArmy([
            new UnitPlacement(5, new Point(19, 5)),
            new UnitPlacement(6, new Point(19, 6)),
            new UnitPlacement(7, new Point(19, 7)),
        ])).State;
    }
}
