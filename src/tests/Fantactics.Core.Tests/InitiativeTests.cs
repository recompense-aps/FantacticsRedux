using Fantactics.Core.Commands;
using Fantactics.Core.Scenarios;
using Fantactics.Core.State;
using static Fantactics.Core.Tests.Play;

namespace Fantactics.Core.Tests;

/// <summary>Action order (GameDesign §4.1): Held +1, Braced +3, and tie-breaking.</summary>
public class InitiativeTests
{
    [Fact]
    public void HoldingGivesPlusOne()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Herbalist", 0, 0, out int herbalist)
            .AddUnit(Seat.P2, "Mauler", 6, 4, out int mauler)
            .WithTiePriority(Seat.P2)
            .Build();

        (GameState after, _) = Moves(state, SubmitMoveOrders.HoldAll, Orders(Move(mauler, (5, 4))));

        Assert.Equal(5, after.TurnState.EffectiveInitiative[herbalist]);
        Assert.Equal(4, after.TurnState.EffectiveInitiative[mauler]);
        Assert.Equal(new[] { herbalist, mauler }, after.TurnState.ActionQueue.ToArray());
    }

    [Fact]
    public void AnyMoveLosesTheHeldBonus()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Herbalist", 0, 0, out int herbalist)
            .AddUnit(Seat.P2, "Mauler", 6, 4)
            .Build();

        (GameState after, _) = Moves(state, Orders(Move(herbalist, (1, 0))), SubmitMoveOrders.HoldAll);

        Assert.Equal(4, after.TurnState.EffectiveInitiative[herbalist]);
        Assert.DoesNotContain(herbalist, after.TurnState.Held);
    }

    [Fact]
    public void BracedArcherActsBeforeTheChargingRusher()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Archer", 0, 2, out int archer)
            .AddUnit(Seat.P2, "Rusher", 6, 2, out int rusher)
            .Build();

        (GameState after, _) = Moves(state, SubmitMoveOrders.HoldAll, Orders(Move(rusher, (5, 2), (4, 2), (3, 2))));

        Assert.Contains(archer, after.TurnState.Braced);
        Assert.Equal(8, after.TurnState.EffectiveInitiative[archer]);
        Assert.Equal(new[] { archer, rusher }, after.TurnState.ActionQueue.ToArray());
    }

    [Fact]
    public void TiesBetweenPlayersAlternateStartingWithTiePriority()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Ranger", 0, 0, out int rangerA)
            .AddUnit(Seat.P1, "Ranger", 0, 4, out int rangerB)
            .AddUnit(Seat.P2, "Rusher", 6, 0, out int rusherA)
            .AddUnit(Seat.P2, "Rusher", 6, 4, out int rusherB)
            .WithTiePriority(Seat.P2)
            .Build();

        (GameState after, _) = Moves(
            state,
            Orders(Move(rangerA, (1, 0)), Move(rangerB, (1, 4))),
            Orders(Move(rusherA, (5, 0)), Move(rusherB, (5, 4))));

        Assert.Equal(new[] { rusherA, rangerA, rusherB, rangerB }, after.TurnState.ActionQueue.ToArray());
    }

    [Fact]
    public void TiePriorityAlternatesEachTurn()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Archer", 0, 0)
            .AddUnit(Seat.P2, "Tank", 6, 4)
            .WithBallast()
            .WithTiePriority(Seat.P1)
            .Build();

        (GameState moved, _) = Moves(state, SubmitMoveOrders.HoldAll, SubmitMoveOrders.HoldAll);
        (GameState next, _) = WaitOutTurn(moved);

        Assert.Equal(2, next.Turn);
        Assert.Equal(Seat.P2, next.TiePriority);
    }
}
