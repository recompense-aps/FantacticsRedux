using System.Collections.Immutable;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Events;
using Fantactics.Core.Geometry;
using Fantactics.Core.Maps;
using Fantactics.Core.Rules;
using Fantactics.Core.Scenarios;
using Fantactics.Core.State;
using static Fantactics.Core.Tests.Play;

namespace Fantactics.Core.Tests;

/// <summary>Rules with three or four seats and teams (GameDesign §3, §4.1, §4.5).</summary>
public class MultiSeatRulesTests
{
    [Fact]
    public void ThreeTeamsOnOneTileFightInTurnAndTheSurvivorTakesTheTile()
    {
        GameState state = ThreeSeats()
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Rusher", 1, 2, out int rusher)
            .AddUnit(Seat.P2, "Grunt", 5, 2, out int grunt)
            .AddUnit(Seat.P3, "Bruiser", 3, 0, out int bruiser)
            // Bystanders far away keep the action phase open, so the clash winners are still on record.
            .AddUnit(Seat.P1, "Grunt", 0, 4)
            .AddUnit(Seat.P2, "Grunt", 6, 4)
            .Build();

        (GameState after, ImmutableArray<GameEvent> events) = MovesAll(state, new()
        {
            [Seat.P1] = Orders(Move(rusher, (2, 2), (3, 2))),
            [Seat.P2] = Orders(Move(grunt, (4, 2), (3, 2))),
            [Seat.P3] = Orders(Move(bruiser, (3, 1), (3, 2))),
        });

        // Highest initiative first (the Rusher), then the tie between Grunt and Bruiser goes by tie order (P2 first).
        Assert.Equal(
            [(rusher, grunt), (rusher, bruiser)],
            events.OfType<ClashMarked>().Select(e => (e.UnitA, e.UnitB)));
        List<ClashResolved> fights = [.. events.OfType<ClashResolved>()];
        Assert.Equal(2, fights.Count);
        Assert.Equal((rusher, grunt), (fights[0].UnitA, fights[0].UnitB));
        int firstWinner = Assert.IsType<int>(fights[0].WinnerId);
        Assert.Equal((firstWinner, bruiser), (fights[1].UnitA, fights[1].UnitB));
        int survivor = Assert.IsType<int>(fights[1].WinnerId);
        Assert.Equal(new Point(3, 2), after.Units[survivor].Position);
        Assert.Single(new[] { rusher, grunt, bruiser }, after.Units.ContainsKey);
        Assert.Contains(survivor, after.TurnState.ClashWinners);
    }

    [Fact]
    public void TeammatesEnteringTheSameTileDontClash()
    {
        GameState state = ThreeSeats()
            .WithTeam(Seat.P1, 1)
            .WithTeam(Seat.P2, 1)
            .WithTeam(Seat.P3, 2)
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Ranger", 1, 2, out int ranger)
            .AddUnit(Seat.P2, "Grunt", 5, 2, out int grunt)
            .AddUnit(Seat.P3, "Grunt", 6, 4)
            .Build();

        (GameState after, ImmutableArray<GameEvent> events) = MovesAll(state, new()
        {
            [Seat.P1] = Orders(Move(ranger, (2, 2), (3, 2))),
            [Seat.P2] = Orders(Move(grunt, (4, 2), (3, 2))),
        });

        Assert.DoesNotContain(events, e => e is ClashMarked);
        // Friendly collision: the higher initiative (the Ranger) keeps the tile and the Grunt backs up.
        Assert.Equal(new Point(3, 2), after.Units[ranger].Position);
        Assert.Equal(new Point(4, 2), after.Units[grunt].Position);
    }

    [Fact]
    public void UnitsPassThroughTeammatesAndSwapWithThem()
    {
        GameState state = ThreeSeats()
            .WithTeam(Seat.P1, 1)
            .WithTeam(Seat.P2, 1)
            .WithTeam(Seat.P3, 2)
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Ranger", 0, 2, out int ranger)
            .AddUnit(Seat.P2, "Grunt", 2, 2)
            .AddUnit(Seat.P1, "Archer", 0, 4, out int archer)
            .AddUnit(Seat.P2, "Mauler", 1, 4, out int mauler)
            .AddUnit(Seat.P3, "Grunt", 6, 0)
            .Build();

        (GameState after, ImmutableArray<GameEvent> events) = MovesAll(state, new()
        {
            [Seat.P1] = Orders(Move(ranger, (1, 2), (2, 2), (3, 2)), Move(archer, (1, 4))),
            [Seat.P2] = Orders(Move(mauler, (0, 4))),
        });

        Assert.DoesNotContain(events, e => e is ClashMarked);
        Assert.Equal(new Point(3, 2), after.Units[ranger].Position);
        Assert.Equal(new Point(1, 4), after.Units[archer].Position);
        Assert.Equal(new Point(0, 4), after.Units[mauler].Position);
    }

    [Fact]
    public void EnemiesOfAnyTwoSeatsClashWhenTheySwap()
    {
        GameState state = ThreeSeats()
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Grunt", 2, 2, out int p1)
            .AddUnit(Seat.P3, "Grunt", 3, 2, out int p3)
            .AddUnit(Seat.P2, "Grunt", 6, 4)
            .Build();

        (_, ImmutableArray<GameEvent> events) = MovesAll(state, new()
        {
            [Seat.P1] = Orders(Move(p1, (3, 2))),
            [Seat.P3] = Orders(Move(p3, (2, 2))),
        });

        ClashMarked clash = Assert.Single(events.OfType<ClashMarked>());
        Assert.Null(clash.Tile);
        Assert.Equal([p1, p3], new[] { clash.UnitA, clash.UnitB }.Order());
    }

    [Fact]
    public void TeammatesNeitherAttackNorHurtEachOtherButSupportEachOther()
    {
        GameState state = ThreeSeats()
            .WithTeam(Seat.P1, 1)
            .WithTeam(Seat.P2, 1)
            .WithTeam(Seat.P3, 2)
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Grunt", 2, 2, out int attacker)
            .AddUnit(Seat.P2, "Grunt", 3, 1, out int teammate)
            .AddUnit(Seat.P3, "Archer", 3, 2, out int target)
            .Build();
        GameState alone = state with { Units = state.Units.Remove(teammate) };

        int supported = CombatRules.Damage(state, state.Units[attacker], state.Units[target], AttackKind.Basic);
        int unsupported = CombatRules.Damage(alone, alone.Units[attacker], alone.Units[target], AttackKind.Basic);

        Assert.Equal(unsupported + 1, supported);
        Assert.Null(CombatRules.BasicAttackKind(state, state.Units[attacker], state.Units[teammate]));
    }

    [Fact]
    public void TiePriorityRotatesThroughEverySeat()
    {
        GameState state = ThreeSeats()
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Grunt", 0, 0)
            .AddUnit(Seat.P2, "Grunt", 6, 0)
            .AddUnit(Seat.P3, "Grunt", 3, 4)
            .WithBallast()
            .AddReserve(Seat.P3, "WarLord", out _)
            .AddReserve(Seat.P3, "Shaman", out _)
            .WithTiePriority(Seat.P2)
            .Build();

        List<Seat> priorities = [];
        for (int turn = 0; turn < 3; turn++)
        {
            (GameState moved, _) = MovesAll(state, []);
            (state, List<GameEvent> events) = WaitOutTurn(moved);
            priorities.Add(Assert.Single(events.OfType<TurnStarted>()).TiePriority);
        }

        Assert.Equal([Seat.P3, Seat.P1, Seat.P2], priorities);
    }

    [Fact]
    public void InitiativeTiesTakeTurnsAcrossSeatsStartingWithPriority()
    {
        GameState state = ThreeSeats()
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Grunt", 0, 0, out int p1)
            .AddUnit(Seat.P2, "Grunt", 6, 0, out int p2)
            .AddUnit(Seat.P3, "Grunt", 3, 4, out int p3)
            .AddUnit(Seat.P2, "Grunt", 6, 4, out int p2Second)
            .WithTiePriority(Seat.P2)
            .Build();

        (GameState after, _) = MovesAll(state, []);

        Assert.Equal<int>([p2, p3, p1, p2Second], after.TurnState.ActionQueue);
    }

    [Fact]
    public void ARoutedSeatIsEliminatedWhileTwoTeamsPlayOn()
    {
        GameState state = ThreeSeats()
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Grunt", 0, 0)
            .AddUnit(Seat.P2, "Grunt", 6, 0)
            .AddUnit(Seat.P3, "Grunt", 3, 4, out int doomed)
            .WithBallast()
            .WithTiePriority(Seat.P2)
            .Build();

        (GameState moved, _) = MovesAll(state, []);
        (GameState after, List<GameEvent> events) = WaitOutTurn(moved);

        SeatEliminated eliminated = Assert.Single(events.OfType<SeatEliminated>());
        Assert.Equal(Seat.P3, eliminated.Seat);
        Assert.Equal<int>([doomed], eliminated.FieldUnits);
        Assert.DoesNotContain(after.Units.Values, unit => unit.Owner == Seat.P3);
        Assert.Equal(1, after.Players[Seat.P3].EliminatedOnTurn);
        Assert.Null(after.Outcome);
        Assert.Equal(Phase.Movement, after.Phase);
        // P3 would have had priority on turn 2; it passes to the next live seat.
        Assert.Equal(Seat.P1, after.TiePriority);
        Assert.Equal([Seat.P1, Seat.P2], GameEngine.PendingDecisions(after).Select(d => d.Seat));
        Assert.Empty(Invariants.Check(after));
    }

    [Fact]
    public void TheLastTeamStandingWinsWithoutRemovingTheLosers()
    {
        GameState state = ThreeSeats()
            .WithTeam(Seat.P1, 1)
            .WithTeam(Seat.P2, 1)
            .WithTeam(Seat.P3, 2)
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Grunt", 0, 0)
            .AddUnit(Seat.P2, "Grunt", 6, 0)
            .AddUnit(Seat.P3, "Grunt", 3, 4, out int loser)
            .WithBallast()
            .Build();

        (GameState moved, _) = MovesAll(state, []);
        (GameState after, List<GameEvent> events) = WaitOutTurn(moved);

        MatchOutcome outcome = Assert.IsType<MatchOutcome>(after.Outcome);
        Assert.Equal(EndReason.Rout, outcome.Reason);
        Assert.Equal<Seat>([Seat.P1, Seat.P2], outcome.Winners);
        Assert.Equal(
            new Dictionary<Seat, int> { [Seat.P1] = 1, [Seat.P2] = 1, [Seat.P3] = 2 },
            outcome.Placings!);
        Assert.DoesNotContain(events, e => e is SeatEliminated);
        Assert.True(after.Units.ContainsKey(loser));
        Assert.Equal<Seat>([Seat.P1, Seat.P2], Assert.Single(events.OfType<MatchEnded>()).Winners);
    }

    [Fact]
    public void AtTheTurnLimitTeammatesScoresAddUp()
    {
        GameState built = ThreeSeats()
            .WithTeam(Seat.P1, 1)
            .WithTeam(Seat.P2, 2)
            .WithTeam(Seat.P3, 1)
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Grunt", 0, 0)
            .AddUnit(Seat.P2, "Grunt", 6, 0)
            .AddUnit(Seat.P3, "Grunt", 3, 4)
            .WithBallast()
            .AddReserve(Seat.P3, "WarLord", out _)
            .AddReserve(Seat.P3, "Shaman", out _)
            .WithTurn(RulesConfig.Default.TurnLimit)
            .Build();
        GameState state = built
            .WithPlayer(built.Players[Seat.P1] with { DestroyedValue = 3 })
            .WithPlayer(built.Players[Seat.P2] with { DestroyedValue = 5 })
            .WithPlayer(built.Players[Seat.P3] with { DestroyedValue = 3 });

        (GameState moved, _) = MovesAll(state, []);
        (GameState after, _) = WaitOutTurn(moved);

        MatchOutcome outcome = Assert.IsType<MatchOutcome>(after.Outcome);
        Assert.Equal(EndReason.TurnLimit, outcome.Reason);
        Assert.Equal<Seat>([Seat.P1, Seat.P3], outcome.Winners);
        Assert.Equal(2, outcome.Placings![Seat.P2]);
    }

    [Fact]
    public void TeammatesDeployingOnTheSameTileLeaveTheHigherSeatInReserve()
    {
        GameState state = ThreeSeats()
            .WithTeam(Seat.P1, 1)
            .WithTeam(Seat.P2, 1)
            .WithTeam(Seat.P3, 2)
            .WithMap(".......", ".......", ".......", "@seats 3", "@deploy P1 0,0-0,2", "@deploy P2 0,0-0,2", "@deploy P3 6,0-6,2")
            .AddUnit(Seat.P3, "Grunt", 6, 0)
            .AddReserve(Seat.P1, "Grunt", out int first)
            .AddReserve(Seat.P2, "Grunt", out int second)
            .WithCommand(Seat.P1, 4)
            .WithCommand(Seat.P2, 4)
            .Build();

        (GameState after, ImmutableArray<GameEvent> events) = MovesAll(state, new()
        {
            [Seat.P1] = new SubmitMoveOrders([], [new DeployOrder(first, new Point(0, 1))]),
            [Seat.P2] = new SubmitMoveOrders([], [new DeployOrder(second, new Point(0, 1))]),
        });

        Assert.Equal(UnitLocation.Field, after.Units[first].Location);
        Assert.Equal(UnitLocation.Reserve, after.Units[second].Location);
        Assert.Equal(4, after.Players[Seat.P2].Command);
        Assert.DoesNotContain(events, e => e is ClashMarked);
    }

    [Fact]
    public void EachOtherSeatsUnitsGetTheirOwnIdBlock()
    {
        GameState state = ThreeSeats()
            .WithMap(OpenField)
            .AddUnit(Seat.P2, "Grunt", 6, 0, out int p2)
            .AddUnit(Seat.P3, "Grunt", 3, 4, out int p3)
            .AddUnit(Seat.P1, "Grunt", 0, 0, out int p1)
            .AddUnit(Seat.P3, "Grunt", 4, 4, out int p3Second)
            .Build();

        ViewIds ids = ViewIds.For(state, Seat.P1);

        Assert.Equal(1, ids.ToView(p1));
        Assert.Equal(ViewIds.EnemyIdBase + 1, ids.ToView(p2));
        Assert.Equal(2 * ViewIds.EnemyIdBase + 1, ids.ToView(p3));
        Assert.Equal(2 * ViewIds.EnemyIdBase + 2, ids.ToView(p3Second));
    }

    [Fact]
    public void NewMatchesMustFitTheMapAndHaveTwoTeams()
    {
        GameMap riverford = MapLibrary.Load("riverford");
        GameMap crossroads = MapLibrary.Load("crossroads");
        Seat[] four = [Seat.P1, Seat.P2, Seat.P3, Seat.P4];

        GameState match = GameEngine.NewMatch(
            RulesConfig.Default,
            crossroads,
            1,
            four,
            new Dictionary<Seat, int> { [Seat.P1] = 1, [Seat.P2] = 2, [Seat.P3] = 1, [Seat.P4] = 2 });

        Assert.Equal(four, match.Seats);
        Assert.True(match.AreEnemies(Seat.P1, Seat.P2));
        Assert.False(match.AreEnemies(Seat.P1, Seat.P3));
        Assert.Equal([Seat.P2, Seat.P4], match.Opponents(Seat.P1));
        Assert.Throws<ArgumentException>(() =>
            GameEngine.NewMatch(RulesConfig.Default, riverford, 1, [Seat.P1, Seat.P2, Seat.P3], null));
        Assert.Throws<ArgumentException>(() =>
            GameEngine.NewMatch(RulesConfig.Default, crossroads, 1, [Seat.P1], null));
        Assert.Throws<ArgumentException>(() => GameEngine.NewMatch(
            RulesConfig.Default,
            crossroads,
            1,
            [Seat.P1, Seat.P2],
            new Dictionary<Seat, int> { [Seat.P1] = 1, [Seat.P2] = 1 }));
        Assert.Throws<InvalidOperationException>(() => match.SoleOpponent(Seat.P1));
    }

    private static ScenarioBuilder ThreeSeats() => new ScenarioBuilder().WithSeats(Seat.P1, Seat.P2, Seat.P3);

    /// <summary>Submits every live seat's move orders (missing seats hold) and returns the resolved state.</summary>
    private static (GameState State, ImmutableArray<GameEvent> Events) MovesAll(
        GameState state,
        Dictionary<Seat, SubmitMoveOrders> orders)
    {
        List<GameEvent> events = [];
        foreach (Seat seat in state.LiveSeats.ToList())
        {
            Accepted accepted = Apply(state, seat, orders.GetValueOrDefault(seat, SubmitMoveOrders.HoldAll));
            state = accepted.State;
            events.AddRange(accepted.Events);
        }

        return (state, [.. events]);
    }
}
