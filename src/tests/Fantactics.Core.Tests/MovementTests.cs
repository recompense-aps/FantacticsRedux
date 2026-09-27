using System.Collections.Immutable;
using Fantactics.Core.Commands;
using Fantactics.Core.Events;
using Fantactics.Core.Geometry;
using Fantactics.Core.Scenarios;
using Fantactics.Core.State;
using static Fantactics.Core.Tests.Play;

namespace Fantactics.Core.Tests;

/// <summary>Movement resolution rules (GameDesign §4.1, decided 2026-09-27).</summary>
public class MovementTests
{
    [Fact]
    public void ZoneOfControlStopsAUnitThatBecomesAdjacentToAnEnemy()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Ranger", 0, 2, out int ranger)
            .AddUnit(Seat.P2, "Grunt", 3, 1)
            .Build();

        (GameState after, ImmutableArray<GameEvent> events) = Moves(
            state,
            Orders(Move(ranger, (1, 2), (2, 2), (3, 2), (4, 2))),
            SubmitMoveOrders.HoldAll);

        Assert.Equal(new Point(3, 2), after.Units[ranger].Position);
        Assert.Contains(events, e => e is UnitStopped { Reason: StopReason.ZoneOfControl } s && s.UnitId == ranger);
    }

    [Fact]
    public void AUnitStartingNextToAnEnemyCanMoveAway()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Ranger", 2, 2, out int ranger)
            .AddUnit(Seat.P2, "Grunt", 3, 2)
            .Build();

        (GameState after, _) = Moves(state, Orders(Move(ranger, (1, 2), (0, 2))), SubmitMoveOrders.HoldAll);

        Assert.Equal(new Point(0, 2), after.Units[ranger].Position);
    }

    [Fact]
    public void EnemiesEnteringTheSameTileClashAndTheWinnerTakesTheTile()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Ranger", 0, 2, out int ranger)
            .AddUnit(Seat.P1, "Archer", 0, 4)
            .AddUnit(Seat.P2, "Grunt", 4, 2, out int grunt)
            .AddUnit(Seat.P2, "Tank", 6, 4)
            .Build();

        (GameState after, ImmutableArray<GameEvent> events) = Moves(
            state,
            Orders(Move(ranger, (1, 2), (2, 2))),
            Orders(Move(grunt, (3, 2), (2, 2))));

        Assert.Contains(events, e => e is ClashMarked { Tile: Point tile } && tile == new Point(2, 2));
        Assert.False(after.Units.ContainsKey(ranger));
        Assert.Equal(new Point(2, 2), after.Units[grunt].Position);
        Assert.Contains(grunt, after.TurnState.ClashWinners);
        Assert.DoesNotContain(grunt, after.TurnState.ActionQueue);
    }

    [Fact]
    public void EnemiesSwappingTilesClashAndTheWinnerTakesTheLosersTile()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Ranger", 2, 2, out int ranger)
            .AddUnit(Seat.P1, "Archer", 0, 4)
            .AddUnit(Seat.P2, "Grunt", 3, 2, out int grunt)
            .AddUnit(Seat.P2, "Tank", 6, 4)
            .Build();

        (GameState after, ImmutableArray<GameEvent> events) = Moves(
            state,
            Orders(Move(ranger, (3, 2))),
            Orders(Move(grunt, (2, 2))));

        Assert.Contains(events, e => e is ClashMarked { Tile: null });
        Assert.False(after.Units.ContainsKey(ranger));
        Assert.Equal(new Point(2, 2), after.Units[grunt].Position);
    }

    [Fact]
    public void ThreeUnitsOnOneTileReduceToOneContestantPerSide()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Ranger", 0, 2, out int ranger)
            .AddUnit(Seat.P1, "Herbalist", 2, 0, out int herbalist)
            .AddUnit(Seat.P2, "Grunt", 4, 2, out int grunt)
            .Build();

        (GameState after, ImmutableArray<GameEvent> events) = Moves(
            state,
            Orders(Move(ranger, (1, 2), (2, 2)), Move(herbalist, (2, 1), (2, 2), (2, 3))),
            Orders(Move(grunt, (3, 2), (2, 2))));

        ClashMarked clash = Assert.Single(events.OfType<ClashMarked>());
        Assert.Equal([ranger, grunt], new[] { clash.UnitA, clash.UnitB }.Order().ToArray());
        Assert.Equal(new Point(2, 1), after.Units[herbalist].Position);
    }

    [Fact]
    public void FriendlyUnitsPassThroughEachOther()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Ranger", 0, 2, out int ranger)
            .AddUnit(Seat.P1, "Archer", 1, 2, out int archer)
            .AddUnit(Seat.P2, "Grunt", 6, 4)
            .Build();

        (GameState after, _) = Moves(state, Orders(Move(ranger, (1, 2), (2, 2), (3, 2))), SubmitMoveOrders.HoldAll);

        Assert.Equal(new Point(3, 2), after.Units[ranger].Position);
        Assert.Equal(new Point(1, 2), after.Units[archer].Position);
    }

    [Fact]
    public void AUnitForcedToStopOnAFriendlyTileBacksUp()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Ranger", 0, 2, out int ranger)
            .AddUnit(Seat.P1, "Archer", 2, 2)
            .AddUnit(Seat.P2, "Grunt", 2, 1)
            .Build();

        (GameState after, ImmutableArray<GameEvent> events) = Moves(
            state,
            Orders(Move(ranger, (1, 2), (2, 2), (3, 2))),
            SubmitMoveOrders.HoldAll);

        Assert.Equal(new Point(1, 2), after.Units[ranger].Position);
        Assert.Contains(events, e => e is UnitStopped { Reason: StopReason.BackedUp } s && s.UnitId == ranger);
    }

    [Fact]
    public void AUnitBlockedWhilePassingThroughAFriendBacksUpAndTheFriendStays()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Archer", 0, 2, out int archer)
            .AddUnit(Seat.P1, "Scout", 1, 2, out int scout)
            .AddUnit(Seat.P1, "Ranger", 2, 0, out int ranger)
            .AddUnit(Seat.P2, "Grunt", 2, 4, out int grunt)
            .Build();

        // Tick 1: the Archer steps onto the Scout's tile. Tick 2: the Ranger takes the clash for (2,2), which
        // blocks the Archer on the Scout's tile, so the Archer backs up.
        (GameState after, _) = Moves(
            state,
            Orders(Move(archer, (1, 2), (2, 2), (3, 2)), Move(ranger, (2, 1), (2, 2))),
            Orders(Move(grunt, (2, 3), (2, 2))));

        Assert.Equal(new Point(1, 2), after.Units[scout].Position);
        Assert.Equal(new Point(0, 2), after.Units[archer].Position);
    }

    [Fact]
    public void FriendlyUnitsEndingOnTheSameTileGoToTheHigherInitiative()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Archer", 1, 3, out int archer)
            .AddUnit(Seat.P1, "Ranger", 1, 1, out int ranger)
            .AddUnit(Seat.P2, "Grunt", 6, 4)
            .Build();

        (GameState after, _) = Moves(
            state,
            Orders(Move(archer, (1, 2)), Move(ranger, (1, 2))),
            SubmitMoveOrders.HoldAll);

        Assert.Equal(new Point(1, 2), after.Units[ranger].Position);
        Assert.Equal(new Point(1, 3), after.Units[archer].Position);
    }

    [Fact]
    public void SlipperyScoutRetreatsInsteadOfClashing()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Scout", 0, 2, out int scout)
            .AddUnit(Seat.P2, "Grunt", 4, 2, out int grunt)
            .Build();

        (GameState after, ImmutableArray<GameEvent> events) = Moves(
            state,
            Orders(Move(scout, (1, 2), (2, 2))),
            Orders(Move(grunt, (3, 2), (2, 2))));

        Assert.Contains(events, e => e is ClashAvoided a && a.UnitId == scout);
        Assert.DoesNotContain(events, e => e is ClashMarked);
        Assert.Equal(new Point(1, 2), after.Units[scout].Position);
        Assert.Equal(new Point(2, 2), after.Units[grunt].Position);
    }

    [Fact]
    public void ClashesIgnoreOnHitEffectsSoTwoTanksFightToTheDeath()
    {
        // Each Tank deals 1 per strike. With Bloodthirst 3 they'd heal forever; clashes are pure fighting.
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .WithRaces("Goblins", "Goblins")
            .AddUnit(Seat.P1, "Tank", 0, 2, out int tankA)
            .AddUnit(Seat.P2, "Tank", 4, 2, out int tankB)
            .WithTiePriority(Seat.P1)
            .Build();

        (GameState after, ImmutableArray<GameEvent> events) = Moves(
            state,
            Orders(Move(tankA, (1, 2), (2, 2))),
            Orders(Move(tankB, (3, 2), (2, 2))));

        ClashResolved resolved = Assert.Single(events.OfType<ClashResolved>());
        Assert.Equal(tankA, resolved.WinnerId);
        Assert.DoesNotContain(events, e => e is UnitHealed or StatusApplied);
        Assert.Equal(new Point(2, 2), after.Units[tankA].Position);
        Assert.Equal(1, after.Units[tankA].Hp);
    }

    [Fact]
    public void MaulersDontHamstringInClashes()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Druid", 0, 2, out int druid)
            .AddUnit(Seat.P2, "Mauler", 4, 2, out int mauler)
            .Build();

        (_, ImmutableArray<GameEvent> events) = Moves(
            state,
            Orders(Move(druid, (1, 2), (2, 2))),
            Orders(Move(mauler, (3, 2), (2, 2))));

        Assert.Contains(events, e => e is UnitAttacked { Kind: AttackKind.Clash } a && a.AttackerId == mauler);
        Assert.DoesNotContain(events, e => e is StatusApplied);
    }

    [Fact]
    public void ArrivalsOnTheSameTileClashAndTheWinnerStays()
    {
        // On a 5-wide map the 3-column deploy zones overlap at x=2, so both sides may arrive on (2,0).
        GameState state = new ScenarioBuilder()
            .WithMap(".....", ".....", ".....", ".....", ".....")
            .WithTurn(2)
            .WithTiePriority(Seat.P2)
            .WithCommand(Seat.P1, 6)
            .WithCommand(Seat.P2, 6)
            .AddUnit(Seat.P1, "Archer", 0, 4)
            .AddReserve(Seat.P1, "Ranger", out int ranger)
            .AddUnit(Seat.P2, "Tank", 4, 4)
            .AddReserve(Seat.P2, "Rusher", out int rusher)
            .WithBallast()
            .Build();

        (GameState after, ImmutableArray<GameEvent> events) = Moves(
            state,
            new SubmitMoveOrders([], [new DeployOrder(ranger, new Point(2, 0))]),
            new SubmitMoveOrders([], [new DeployOrder(rusher, new Point(2, 0))]));

        // Tied initiative 6, P2 has priority: Rusher 3+1 Reckless−1 = 3, Ranger floor(3/2) = 1, and so on.
        Assert.Contains(events, e => e is ClashMarked { Tick: 0 });
        Assert.False(after.Units.ContainsKey(ranger));
        Assert.Equal(new Point(2, 0), after.Units[rusher].Position);
        Assert.Equal(2, after.Units[rusher].Hp);
        Assert.Contains(rusher, after.TurnState.ClashWinners);
        Assert.Equal(0, after.Players[Seat.P1].Command);
        Assert.Equal(4, after.Players[Seat.P2].Command);
    }

    [Fact]
    public void MinimumMoveLetsASlowedUnitEnterCostlyTerrain()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(".^.", "...", "...")
            .AddUnit(Seat.P1, "Scout", 0, 2)
            .AddUnit(Seat.P2, "Tank", 1, 1, out int tank)
            .Modify(unit => unit with { Statuses = unit.Statuses.SetItem(StatusKind.Slowed, 1) })
            .Build();

        // Slowed Tank has 1 Movement; mountains cost 2 for goblins, but a single step is always allowed.
        Rejects(state, Seat.P2, Orders(Move(tank, (1, 2), (2, 2))), "too-far");
        (GameState after, _) = Moves(state, SubmitMoveOrders.HoldAll, Orders(Move(tank, (1, 0))));
        Assert.Equal(new Point(1, 0), after.Units[tank].Position);
    }

    [Fact]
    public void PathsLongerThanMovementAreRejected()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Scout", 0, 0)
            .AddUnit(Seat.P2, "Tank", 6, 0, out int tank)
            .Build();

        Rejects(state, Seat.P2, Orders(Move(tank, (5, 0), (4, 0), (3, 0), (2, 0))), "too-far");
    }

    [Fact]
    public void RootedUnitsMustHold()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Scout", 0, 0, out int scout)
            .Modify(unit => unit with { Statuses = unit.Statuses.SetItem(StatusKind.Rooted, 1) })
            .AddUnit(Seat.P2, "Tank", 6, 0)
            .Build();

        Rejects(state, Seat.P1, Orders(Move(scout, (1, 0))), "rooted");
    }

    [Fact]
    public void EndingOnAHoldingFriendIsRejected()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Scout", 0, 0, out int scout)
            .AddUnit(Seat.P1, "Archer", 2, 0)
            .AddUnit(Seat.P2, "Tank", 6, 4)
            .Build();

        Rejects(state, Seat.P1, Orders(Move(scout, (1, 0), (2, 0))), "destination-held");
    }
}
