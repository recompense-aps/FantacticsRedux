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
    public void AClashNeitherSideCanWinEndsAtTheStrikeCapWithBothAlive()
    {
        // Goblins vs Goblins: each Tank deals 1 and heals 3 (Bloodthirst), so neither can die.
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .WithRaces("Goblins", "Goblins")
            .AddUnit(Seat.P1, "Tank", 0, 2, out int tankA)
            .AddUnit(Seat.P2, "Tank", 4, 2, out int tankB)
            .Build();

        (GameState after, ImmutableArray<GameEvent> events) = Moves(
            state,
            Orders(Move(tankA, (1, 2), (2, 2))),
            Orders(Move(tankB, (3, 2), (2, 2))));

        ClashResolved resolved = Assert.Single(events.OfType<ClashResolved>());
        Assert.Null(resolved.WinnerId);
        Assert.Equal(new Point(1, 2), after.Units[tankA].Position);
        Assert.Equal(new Point(3, 2), after.Units[tankB].Position);
        Assert.Null(after.UnitAt(new Point(2, 2)));
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
