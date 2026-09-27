using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Events;
using Fantactics.Core.Geometry;
using Fantactics.Core.Rules;
using Fantactics.Core.Scenarios;
using Fantactics.Core.State;
using static Fantactics.Core.Tests.Play;

namespace Fantactics.Core.Tests;

/// <summary>Action phase: attacks, Retaliate, Delay, abilities, and arrivals (GameDesign §4.2–4.4).</summary>
public class ActionTests
{
    [Fact]
    public void AttackDealsPreviewedDamage()
    {
        GameState state = ActionPhase(
            new ScenarioBuilder()
                .WithMap(OpenField)
                .AddUnit(Seat.P1, "Archer", 0, 2, out int archer)
                .AddUnit(Seat.P2, "Grunt", 3, 2, out int grunt));

        LegalActions legal = Assert.IsType<LegalActions>(LegalActions.For(state, Seat.P1));
        ActionOption option = Assert.Single(legal.Actions, o => o.Command is Attack);
        GameState after = Apply(state, Seat.P1, option.Command).State;

        Assert.Equal(new AttackPreview(grunt, 4, false), option.Preview);
        Assert.Equal(1, after.Units[grunt].Hp);
        Assert.Equal(archer, ((Attack)option.Command).UnitId);
    }

    [Fact]
    public void ArchersCantAttackAdjacentEnemies()
    {
        GameState state = ActionPhase(
            new ScenarioBuilder()
                .WithMap(OpenField)
                .AddUnit(Seat.P1, "Archer", 0, 2, out int archer)
                .AddUnit(Seat.P2, "Grunt", 1, 2, out int grunt));

        Rejects(state, Seat.P1, new Attack(archer, grunt), "out-of-range");
    }

    [Fact]
    public void ForestBlocksGoblinSightButNotElves()
    {
        GameState state = ActionPhase(
            new ScenarioBuilder()
                .WithMap(".%..", "....")
                .AddUnit(Seat.P1, "Archer", 0, 0, out int archer)
                .AddUnit(Seat.P2, "Mauler", 2, 0, out int mauler));

        Assert.IsType<Accepted>(GameEngine.Apply(state, Seat.P1, new Attack(archer, mauler)));
        GameState goblinTurn = state with { TurnState = state.TurnState with { ActionQueue = [mauler] } };
        Rejects(goblinTurn, Seat.P2, new UseAbility(mauler, AbilityIds.ThrowNet, new Point(0, 0)), "no-line-of-sight");
    }

    [Fact]
    public void TankRetaliatesAgainstAPointBlankShotAndHeals()
    {
        GameState state = ActionPhase(
            new ScenarioBuilder()
                .WithMap(OpenField)
                .AddUnit(Seat.P1, "Ranger", 0, 2, out int ranger)
                .AddUnit(Seat.P2, "Tank", 1, 2, out int tank));

        Accepted result = Apply(state, Seat.P1, new Attack(ranger, tank));

        Assert.Contains(result.Events, e => e is UnitAttacked { Kind: AttackKind.Retaliate } a && a.TargetId == ranger);
        Assert.Equal(6, result.State.Units[ranger].Hp);
        Assert.Equal(8, result.State.Units[tank].Hp);
    }

    [Fact]
    public void DelayMovesTheUnitToTheEndOnce()
    {
        GameState state = ActionPhase(
            new ScenarioBuilder()
                .WithMap(OpenField)
                .AddUnit(Seat.P1, "Scout", 0, 0, out int scout)
                .AddUnit(Seat.P2, "Tank", 6, 4, out int tank));

        GameState delayed = Apply(state, Seat.P1, new Delay(scout)).State;

        Assert.Equal(tank, delayed.TurnState.CurrentActor);
        GameState scoutAgain = Apply(delayed, Seat.P2, new Wait(tank)).State;
        Assert.Equal(scout, scoutAgain.TurnState.CurrentActor);
        Rejects(scoutAgain, Seat.P1, new Delay(scout), "already-delayed");
    }

    [Fact]
    public void ActingOutOfTurnIsRejected()
    {
        GameState state = ActionPhase(
            new ScenarioBuilder()
                .WithMap(OpenField)
                .AddUnit(Seat.P1, "Scout", 0, 0)
                .AddUnit(Seat.P2, "Tank", 6, 4, out int tank));

        Rejects(state, Seat.P2, new Wait(tank), "not-your-decision");
    }

    [Fact]
    public void PinningShotRootsAndGoesOnCooldown()
    {
        GameState state = ActionPhase(
            new ScenarioBuilder()
                .WithMap(OpenField)
                .AddUnit(Seat.P1, "Ranger", 0, 2, out int ranger)
                .AddUnit(Seat.P2, "Tank", 3, 2, out int tank));

        GameState after = Apply(state, Seat.P1, new UseAbility(ranger, AbilityIds.PinningShot, new Point(3, 2))).State;

        Assert.Equal(2, after.Units[tank].Statuses[StatusKind.Rooted]);
        Assert.Equal(4, after.Units[ranger].AbilityReadyTurn[AbilityIds.PinningShot]);
    }

    [Fact]
    public void RootLastsThroughTheNextTurnThenExpires()
    {
        GameState state = ActionPhase(
            new ScenarioBuilder()
                .WithMap(OpenField)
                .AddUnit(Seat.P1, "Ranger", 0, 2, out int ranger)
                .AddUnit(Seat.P2, "Tank", 3, 2, out int tank)
                .WithBallast());
        state = Apply(state, Seat.P1, new UseAbility(ranger, AbilityIds.PinningShot, new Point(3, 2))).State;

        (GameState turnTwo, _) = WaitOutTurn(state);
        Rejects(turnTwo, Seat.P2, Orders(Move(tank, (4, 2))), "rooted");

        (GameState moved, _) = Moves(turnTwo, SubmitMoveOrders.HoldAll, SubmitMoveOrders.HoldAll);
        (GameState turnThree, _) = WaitOutTurn(moved);
        Assert.False(turnThree.Units[tank].Has(StatusKind.Rooted));
    }

    [Fact]
    public void SummonedGruntsCantActOnTheirFirstTurn()
    {
        GameState state = ActionPhase(
            new ScenarioBuilder()
                .WithMap(OpenField)
                .AddUnit(Seat.P1, "Archer", 0, 0)
                .AddUnit(Seat.P2, "Shaman", 5, 2, out int shaman),
            tiePriority: Seat.P2);
        state = state with { TurnState = state.TurnState with { ActionQueue = [shaman, .. state.TurnState.ActionQueue.Remove(shaman)] } };

        Accepted result = Apply(state, Seat.P2, new UseAbility(shaman, AbilityIds.CallTheHorde, null));

        List<UnitSummoned> summoned = result.Events.OfType<UnitSummoned>().ToList();
        Assert.Equal(2, summoned.Count);
        Assert.All(summoned, s => Assert.DoesNotContain(s.UnitId, result.State.TurnState.ActionQueue));
        Assert.All(summoned, s => Assert.True(result.State.Units[s.UnitId].IsSummoned));
    }

    [Fact]
    public void ElvesArrivingInForestOutsideTheirZoneCantAct()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(".......", "....%..", ".......")
            .WithTurn(2)
            .WithCommand(Seat.P1, 6)
            .AddUnit(Seat.P1, "Archer", 0, 0)
            .AddReserve(Seat.P1, "Ranger", out int ranger)
            .AddUnit(Seat.P2, "Tank", 6, 2)
            .Build();

        (GameState after, _) = Moves(
            state,
            new SubmitMoveOrders([], [new DeployOrder(ranger, new Point(4, 1))]),
            SubmitMoveOrders.HoldAll);

        Assert.Equal(new Point(4, 1), after.Units[ranger].Position);
        Assert.Equal(0, after.Players[Seat.P1].Command);
        Assert.DoesNotContain(ranger, after.TurnState.ActionQueue);
    }

    [Fact]
    public void GoblinDiscountSkipsCheapUnits()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Archer", 0, 0)
            .AddReserve(Seat.P2, "Grunt", out int grunt)
            .AddReserve(Seat.P2, "Tank", out int tank)
            .Build();

        Assert.Equal(2, UnitRules.DeployCost(state, state.Units[grunt]));
        Assert.Equal(3, UnitRules.DeployCost(state, state.Units[tank]));
    }

    /// <summary>Resolves a hold-everything movement phase so the scenario is in its action phase.</summary>
    private static GameState ActionPhase(ScenarioBuilder builder, Seat tiePriority = Seat.P1) =>
        Moves(builder.WithTiePriority(tiePriority).Build(), SubmitMoveOrders.HoldAll, SubmitMoveOrders.HoldAll).State;
}
