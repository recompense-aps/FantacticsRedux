using System.Runtime.CompilerServices;
using Fantactics.Client.Logic.Log;
using Fantactics.Client.Logic.Session;
using Fantactics.Core;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Events;
using Fantactics.Core.Geometry;
using Fantactics.Core.Hosting;
using Fantactics.Core.Players;
using Fantactics.Core.Rules;
using Fantactics.Protocol.Connections;

namespace Fantactics.Client.Logic.Tests;

/// <summary>The player's log: events in words, per seat, in step with playback.</summary>
public class EventLogTests
{
    public static IEnumerable<object[]> EventTypes() => TimelineTests.EventTypes();

    [Theory]
    [MemberData(nameof(EventTypes))]
    public void EveryEventTypeHasTextOrIsLeftOutOnPurpose(Type type)
    {
        GameEvent sample = (GameEvent)RuntimeHelpers.GetUninitializedObject(type);

        Exception? problem = Record.Exception(() => EventText.Describe(sample, new UnitNames()));

        Assert.IsNotType<NotSupportedException>(problem);
    }

    [Fact]
    public void StrikesNameBothUnitsAndHowTheyStruck()
    {
        UnitNames names = Names();

        Assert.Equal("P1 Archer hits P2 Wolf Rider for 3, 4 HP left", Text(new UnitAttacked(1, 2, AttackKind.Basic, 3, 4), names));
        Assert.Equal("P1 Archer hits P2 Wolf Rider for 1 (point blank), 3 HP left", Text(new UnitAttacked(1, 2, AttackKind.PointBlank, 1, 3), names));
        Assert.Equal("P2 Wolf Rider hits P1 Archer for 2 (clash), 5 HP left", Text(new UnitAttacked(2, 1, AttackKind.Clash, 2, 5), names));
        Assert.Equal("P2 Wolf Rider hits P1 Archer for 2 (retaliate), 3 HP left", Text(new UnitAttacked(2, 1, AttackKind.Retaliate, 2, 3), names));
    }

    [Fact]
    public void ArrivalsAreNamedFromTheEventAndDeadUnitsKeepTheirNames()
    {
        UnitNames names = Names();

        string[] lines = [.. EventText.Lines(
            [
                new UnitArrived(9, Seat.P2, "Shaman", new Point(8, 1), 3, false),
                new UnitDied(9, 1, 3),
            ],
            names,
            turn: 4).Select(line => line.Text)];

        Assert.Equal(
            ["P2 Shaman deploys at (8,1) for 3 Command, can't act this turn", "P2 Shaman is destroyed by P1 Archer"],
            lines);
    }

    [Fact]
    public void ARunOfStepsIsOneLinePerUnitAndARunOfPlacementsOneLinePerSeat()
    {
        UnitNames names = Names();

        LogLine[] lines = [.. EventText.Lines(
            [
                new UnitPlaced(3, Seat.P1, "Spearman", new Point(0, 0)),
                new UnitPlaced(4, Seat.P1, "Spearman", new Point(0, 1)),
                new UnitPlaced(5, Seat.P2, "Grunt", new Point(9, 0)),
                new TurnStarted(2, Seat.P1),
                new UnitStepped(1, 1, new Point(1, 0), new Point(2, 0)),
                new UnitStepped(1, 2, new Point(5, 5), new Point(4, 5)),
                new UnitStopped(1, 2, new Point(4, 5), StopReason.ZoneOfControl),
                new UnitStepped(2, 1, new Point(2, 0), new Point(3, 0)),
                new ClashMarked(3, 1, 2, new Point(3, 1)),
            ],
            names,
            turn: 1)];

        Assert.Equal(
            [
                (1, "P1 places 2 units"),
                (1, "P2 places 1 unit"),
                (2, "Turn 2"),
                (2, "P1 Archer moves (1,0) → (3,0)"),
                (2, "P2 Wolf Rider moves (5,5) → (4,5)"),
                (2, "P1 Archer and P2 Wolf Rider clash at (3,1)"),
            ],
            lines.Select(line => (line.Turn, line.Text)));
        Assert.Equal("P1 Spearman", names[4]);
        Assert.Equal(["Turn 2"], lines.Where(line => line.Heading).Select(line => line.Text));
    }

    [Fact]
    public void AnUpdateIsLoggedOnceAndReadOnlyUpToTheUpdateOnScreen()
    {
        MatchHost host = new(RulesConfig.Default, States.Setup(11));
        EventLog twice = new(host.Snapshot(Seat.P1).View);
        EventLog once = new(host.Snapshot(Seat.P1).View);
        List<SeatUpdate> updates = [];
        host.Updated += (seat, update) =>
        {
            if (seat == Seat.P1)
            {
                updates.Add(update);
                twice.Append(update);
                twice.Append(update);
                once.Append(update);
            }
        };

        PlayUntil(host, () => host.State.Turn >= 2);
        SeatUpdate first = updates.First(update => twice.Through(update).Count > 0);
        IReadOnlyList<LogLine> all = twice.Through(updates[^1]);

        Assert.True(twice.Through(first).Count < all.Count);
        Assert.Equal(once.Through(updates[^1]), all);
        Assert.Equal(all, twice.Through(host.Snapshot(Seat.P1)));
        Assert.Contains(all, line => line.Text == "Placement phase");
        Assert.Contains(all, line => line.Text == "Movement phase");
    }

    [Fact]
    public void AWholeBotMatchReadsWithEveryUnitNamed()
    {
        MatchHost host = new(RulesConfig.Default, States.Setup(12));
        Dictionary<Seat, EventLog> logs = host.State.Seats.ToDictionary(seat => seat, seat => new EventLog(host.Snapshot(seat).View));
        Dictionary<Seat, SeatUpdate> last = [];
        host.Updated += (seat, update) =>
        {
            logs[seat].Append(update);
            last[seat] = update;
        };

        PlayUntil(host, () => host.IsOver);

        foreach ((Seat seat, EventLog log) in logs)
        {
            string[] text = [.. log.Through(last[seat]).Select(line => line.Text)];
            Assert.DoesNotContain(text, line => line.Contains("unit ", StringComparison.Ordinal) && !line.Contains("places", StringComparison.Ordinal));
            Assert.Contains("Turn 2", text);
            Assert.Contains("Action phase", text);
            Assert.Contains(text, line => line.Contains(" hits ", StringComparison.Ordinal));
            Assert.Contains(text, line => line.Contains(" wins (", StringComparison.Ordinal) || line.StartsWith("Draw", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task InHotseatEachSeatsLogHasEveryUpdateOnceIncludingTheResentOne()
    {
        LocalMatch match = new(new MatchHost(RulesConfig.Default, States.Setup(4)), States.CreateBot);
        ClientSession session = new(match, RulesConfig.Default, States.CreateBot, "captain");
        await session.StartAsync();
        for (int step = 0; step < 50 && session.Current.Legal?.Decision is not SubmitMoveOrdersDecision; step++)
        {
            await Task.Delay(20);
        }

        Seat first = session.Shown;
        IReadOnlyList<LogLine> before = session.LogFor(session.Current);
        await session.SubmitAsync(SubmitMoveOrders.HoldAll);
        SeatUpdate resent = session.Current;

        Assert.NotEqual(first, resent.View.Seat);
        Assert.Contains(before, line => line.Text == "Movement phase");
        Assert.Contains(session.LogFor(resent), line => line.Text == $"{first} locked in move orders");
        Assert.Equal(session.LogFor(resent), session.LogFor(session.Current));
    }

    private static UnitNames Names()
    {
        UnitNames names = new();
        names.Learn(1, Seat.P1, "Archer");
        names.Learn(2, Seat.P2, "WolfRider");
        return names;
    }

    private static string? Text(GameEvent gameEvent, UnitNames names) => EventText.Describe(gameEvent, names);

    private static void PlayUntil(MatchHost host, Func<bool> done)
    {
        Dictionary<Seat, IPlayerAgent> bots = new()
        {
            [Seat.P1] = States.CreateBot("captain", 1),
            [Seat.P2] = States.CreateBot("berserker", 2),
        };
        while (!host.IsOver && !done())
        {
            Seat seat = GameEngine.PendingDecisions(host.State)[0].Seat;
            host.SubmitEngine(seat, bots[seat].DecideFor(host.State, seat));
        }
    }
}
