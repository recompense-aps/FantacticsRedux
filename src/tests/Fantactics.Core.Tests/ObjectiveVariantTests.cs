using System.Text.Json;
using Fantactics.Core.Commands;
using Fantactics.Core.Geometry;
using Fantactics.Core.Rules;
using Fantactics.Core.Scenarios;
using Fantactics.Core.Serialization;
using Fantactics.Core.State;
using static Fantactics.Core.Tests.Play;

namespace Fantactics.Core.Tests;

/// <summary>The objective-scoring switches in the rules config (GameDesign §4.5 variants).</summary>
public class ObjectiveVariantTests
{
    [Fact]
    public void PerTileScoringPaysEachPlayerForEveryTileHeld()
    {
        RulesConfig rules = RulesConfig.Default with
        {
            ObjectiveScoring = ObjectiveScoring.PerTile,
            ObjectivePointsPerTurn = 1,
        };
        GameState state = new ScenarioBuilder(rules)
            .WithMap(OpenField)
            .WithObjectives((3, 1), (4, 1), (3, 3))
            .AddUnit(Seat.P1, "Druid", 3, 1)
            .AddUnit(Seat.P1, "Ranger", 4, 1)
            .AddUnit(Seat.P2, "WarLord", 3, 3)
            .WithBallast()
            .Build();

        (GameState moved, _) = Moves(state, SubmitMoveOrders.HoldAll, SubmitMoveOrders.HoldAll);
        (GameState next, _) = WaitOutTurn(moved);

        Assert.Equal(2, next.Players[Seat.P1].ObjectivePoints);
        Assert.Equal(1, next.Players[Seat.P2].ObjectivePoints);
    }

    [Fact]
    public void EntrenchedObjectivesOnlyCountUnitsThatHeld()
    {
        RulesConfig rules = RulesConfig.Default with { ObjectivesNeedHold = true };
        GameState state = new ScenarioBuilder(rules)
            .WithMap(OpenField)
            .WithObjectives((3, 1), (4, 1), (3, 3))
            .AddUnit(Seat.P1, "Druid", 2, 1, out int druid)
            .AddUnit(Seat.P1, "Ranger", 5, 1, out int ranger)
            .AddUnit(Seat.P2, "WarLord", 3, 3)
            .WithBallast()
            .Build();
        SubmitMoveOrders p1 = new(
            [new MoveOrder(druid, [new Point(3, 1)]), new MoveOrder(ranger, [new Point(4, 1)])],
            []);

        (GameState moved, _) = Moves(state, p1, SubmitMoveOrders.HoldAll);
        (GameState next, _) = WaitOutTurn(moved);

        // P1 stands on two tiles but moved onto both, so the WarLord's one held tile is the majority.
        Assert.Equal(0, next.Players[Seat.P1].ObjectivePoints);
        Assert.Equal(rules.ObjectivePointsPerTurn, next.Players[Seat.P2].ObjectivePoints);
    }

    [Fact]
    public void TheDefaultsLeaveTheRulesHashUnchanged()
    {
        string json = JsonSerializer.Serialize(RulesConfig.Default, CoreJson.Options);
        RulesConfig roundTripped = RulesConfig.FromJson(json);

        Assert.DoesNotContain("objectiveScoring", json);
        Assert.DoesNotContain("objectivesNeedHold", json);
        Assert.Equal(RulesConfig.Default.Hash, roundTripped.Hash);
        Assert.NotEqual(RulesConfig.Default.Hash, (RulesConfig.Default with { ObjectivesNeedHold = true }).Hash);
    }
}
