using System.Collections.Concurrent;
using Fantactics.Ai.Profiles;
using Fantactics.Core;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Events;
using Fantactics.Core.Hosting;
using Fantactics.Core.Players;
using Fantactics.Core.Records;
using Fantactics.Core.Rules;
using Fantactics.Core.State;
using Fantactics.Protocol.Connections;

namespace Fantactics.Ai.Tests;

/// <summary>The local match host behind <see cref="IGameConnection"/>: hotseat, vs-bot, auto-skip, and queuing.</summary>
public class LocalMatchTests
{
    private const int MaxDecisions = 5_000;

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task HotseatWithAutoSkipPlaysToTheEndAndOnlyShowsSeatIds(int seed)
    {
        LocalMatch match = Match((ulong)seed, "human", "human");
        List<IGameConnection> seats = [.. SeatExtensions.All.Select(match.Connect)];
        foreach (IGameConnection seat in seats)
        {
            await seat.SetAutoSkipAsync(true);
        }

        List<string> problems = [];
        await PlayOut(match, seats, seed, (seat, update) => problems.AddRange(Problems(seat, update)));

        Assert.Empty(problems.Take(10));
        MatchRecord record = match.ToRecord();
        Assert.Null(MatchReplay.Run(RulesConfig.Default, MatchRecord.FromJson(record.ToJson(), RulesConfig.Default)).DriftAtSeq);
    }

    [Fact]
    public async Task AVsBotMatchPlaysToTheEnd()
    {
        LocalMatch match = Match(3, "human", "bot:captain");
        await match.StartAsync();

        await PlayOut(match, [match.Connect(Seat.P1)], 3, (_, _) => { });

        Assert.True(match.IsOver);
    }

    [Fact]
    public async Task QueuedActionsPlayWhenTheirSlotComesUp()
    {
        LocalMatch match = Match(4, "human", "human");
        List<IGameConnection> seats = [.. SeatExtensions.All.Select(match.Connect)];
        Dictionary<Seat, RandomAgent> players = SeatExtensions.All.ToDictionary(seat => seat, seat => new RandomAgent((int)seat));
        (IGameConnection Seat, int UnitId)? queued = null;
        ConcurrentQueue<(Seat Seat, SeatUpdate Update)> updates = [];
        foreach (IGameConnection seat in seats)
        {
            seat.Updated += update => updates.Enqueue((seat.Seat, update));
        }

        for (int step = 0; step < MaxDecisions && queued is null && !match.IsOver; step++)
        {
            IGameConnection owing = seats.First(seat => seat.Current.Legal is not null);
            SeatUpdate current = owing.Current;
            if (current.Legal?.Decision is ChooseUnitActionDecision decision)
            {
                // Another of this seat's units (own ids are below the enemy base) still to act this turn.
                int later = current.View.TurnState.ActionQueue
                    .FirstOrDefault(id => id != decision.UnitId && id < ViewIds.EnemyIdBase);
                if (later > 0)
                {
                    Assert.Null(await owing.QueueAsync(new Wait(later)));
                    queued = (owing, later);
                    break;
                }
            }

            await owing.SubmitAsync(Decide(players[owing.Seat], current));
        }

        Assert.NotNull(queued);
        (IGameConnection queuer, int queuedUnit) = queued.Value;
        updates.Clear();
        int turn = queuer.Current.View.Turn;
        while (!match.IsOver && seats.Any(seat => seat.Current.View.Turn == turn && seat.Current.Legal is not null))
        {
            IGameConnection owing = seats.First(seat => seat.Current.Legal is not null);
            Assert.False(
                owing == queuer && owing.Current.Legal?.Decision is ChooseUnitActionDecision { UnitId: var id } && id == queuedUnit,
                "The seat was asked about a unit whose action was queued.");
            await owing.SubmitAsync(Decide(players[owing.Seat], owing.Current));
        }

        Assert.Contains(
            updates,
            entry => entry.Seat == queuer.Seat && entry.Update.Events.Any(e => e is UnitWaited { UnitId: var id } && id == queuedUnit));
    }

    [Fact]
    public async Task ASavedMatchResumesBranchesAndCanBeHandedToBots()
    {
        LocalMatch original = Match(6, "human", "human");
        List<IGameConnection> seats = [.. SeatExtensions.All.Select(original.Connect)];
        Dictionary<Seat, RandomAgent> players = SeatExtensions.All.ToDictionary(seat => seat, seat => new RandomAgent((int)seat));
        for (int step = 0; step < 40; step++)
        {
            IGameConnection owing = seats.First(seat => seat.Current.Legal is not null);
            await owing.SubmitAsync(Decide(players[owing.Seat], owing.Current));
        }

        MatchRecord saved = MatchRecord.FromJson(original.ToRecord().ToJson(), RulesConfig.Default);
        LocalMatch resumed = new(MatchHost.Resume(RulesConfig.Default, saved), TestMatches.CreateBot);
        LocalMatch branch = original.Branch(20);

        Assert.Null(resumed.ResumeWarning);
        Assert.Equal(StateHash.Compute(original.State), StateHash.Compute(resumed.State));
        Assert.Equal(20, branch.ToRecord().Commands.Length);

        await resumed.SetControllerAsync(Seat.P1, SeatController.Bot("captain"));
        await resumed.SetControllerAsync(Seat.P2, SeatController.Bot("bumble"));

        Assert.True(resumed.IsOver);
        Assert.Equal("bot:captain", resumed.ToRecord().Setup.Seats[Seat.P1]);
    }

    /// <summary>A Riverford match with the given seat labels (<c>human</c>, <c>bot:…</c>).</summary>
    internal static LocalMatch Match(ulong seed, string p1, string p2) => new(
        new MatchHost(RulesConfig.Default, TestMatches.Riverford(seed).WithSeats(p1, p2)),
        TestMatches.CreateBot);

    private static async Task PlayOut(
        LocalMatch match,
        List<IGameConnection> seats,
        int seed,
        Action<Seat, SeatUpdate> check)
    {
        Dictionary<Seat, RandomAgent> players = seats.ToDictionary(seat => seat.Seat, seat => new RandomAgent(seed * 2 + (int)seat.Seat));
        foreach (IGameConnection seat in seats)
        {
            seat.Updated += update => check(seat.Seat, update);
        }

        for (int step = 0; step < MaxDecisions && !match.IsOver; step++)
        {
            IGameConnection owing = seats.FirstOrDefault(seat => seat.Current.Legal is not null)
                ?? throw new InvalidOperationException("Nobody owes a decision but the match isn't over.");
            Assert.Null(await owing.SubmitAsync(Decide(players[owing.Seat], owing.Current)));
        }

        Assert.True(match.IsOver);
    }

    private static ICommand Decide(IPlayerAgent player, SeatUpdate update)
    {
        LegalActions legal = update.Legal ?? throw new InvalidOperationException("No decision owed.");
        return player.Decide(update.View, legal.Decision, legal);
    }

    /// <summary>Checks an update: only seat ids, and with auto-skip no prompt without a real option.</summary>
    private static IEnumerable<string> Problems(Seat seat, SeatUpdate update)
    {
        IEnumerable<string> badIds = update.View.Units
            .Where(unit => unit.Owner == seat ? unit.Id > ViewIds.EnemyIdBase : unit.Id <= ViewIds.EnemyIdBase)
            .Select(unit => $"{seat} sees unit {unit.Id} ({unit.Owner}) with the wrong kind of id.");
        IEnumerable<string> idlePrompts = update.Legal?.Decision is ChooseUnitActionDecision
            && update.Legal.Actions.All(option => option.Command is Wait or Delay)
                ? [$"{seat} was asked to act with nothing to do (turn {update.View.Turn})."]
                : [];
        return badIds.Concat(idlePrompts);
    }
}
