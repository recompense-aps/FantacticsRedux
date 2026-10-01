using System.Collections.Immutable;
using Fantactics.Core;
using Fantactics.Core.Engine;
using Fantactics.Core.Events;
using Fantactics.Core.Records;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Ai.Tests;

/// <summary>Matches with three or four seats and teams, played by bots on Crossroads (GameDesign §3).</summary>
public class MultiSeatTests
{
    public static IEnumerable<object[]> FuzzSeeds => Enumerable.Range(1, 30).Select(seed => new object[] { seed });

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void AFourSeatFreeForAllPlaysToTheEnd(int seed)
    {
        MatchSetup setup = TestMatches.Crossroads((ulong)seed);

        (MatchResult result, List<string> problems, _) = Play(setup, "captain@easy", seed);

        Assert.Empty(problems);
        Assert.InRange(result.Turns, 1, RulesConfig.Default.TurnLimit);
        Assert.InRange(result.Outcome.Winners.Length, 0, 1);
        Assert.Equal(setup.Seats.Keys, result.Outcome.Placings!.Keys);
        Assert.All(result.Outcome.Winners, winner => Assert.Equal(1, result.Outcome.Placings[winner]));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void TwoAgainstTwoPlaysToTheEndWithoutFriendlyFire(int seed)
    {
        MatchSetup setup = TestMatches.Crossroads((ulong)seed, 4, 1, 2, 1, 2);

        (MatchResult result, List<string> problems, List<GameEvent> events) = Play(setup, "captain@easy", seed);

        Assert.Empty(problems);
        Assert.True(
            result.Outcome.Winners is [] or [Seat.P1, Seat.P3] or [Seat.P2, Seat.P4],
            $"Winners {string.Join('+', result.Outcome.Winners)} aren't one whole team.");
        Assert.Equal(result.Outcome.Placings![Seat.P1], result.Outcome.Placings[Seat.P3]);
        Assert.Equal(result.Outcome.Placings[Seat.P2], result.Outcome.Placings[Seat.P4]);

        GameState start = setup.CreateInitialState(RulesConfig.Default);
        Dictionary<int, Seat> owners = result.FinalState.Owners.ToDictionary();
        Assert.DoesNotContain(
            events.OfType<UnitAttacked>(),
            attack => !start.AreEnemies(owners[attack.AttackerId], owners[attack.TargetId]));
    }

    [Theory]
    [MemberData(nameof(FuzzSeeds))]
    public void RandomMultiSeatMatchesKeepInvariantsAndEnd(int seed)
    {
        // Rotate through three-seat, four-seat free-for-all, and two-against-two.
        MatchSetup setup = (seed % 3) switch
        {
            0 => TestMatches.Crossroads((ulong)seed, 3),
            1 => TestMatches.Crossroads((ulong)seed),
            _ => TestMatches.Crossroads((ulong)seed, 4, 1, 2, 1, 2),
        };

        (MatchResult result, List<string> problems, _) = Play(setup, "random", seed);

        Assert.Empty(problems);
        Assert.InRange(result.Turns, 1, RulesConfig.Default.TurnLimit);
    }

    [Fact]
    public void FourSeatMatchesAreDeterministicAndReplayWithoutDrift()
    {
        MatchSetup setup = TestMatches.Crossroads(5, 4, 1, 2, 1, 2);
        string[] hashes = Enumerable.Range(0, 3)
            .AsParallel()
            .Select(_ => StateHash.Compute(MatchRunner.Run(
                RulesConfig.Default,
                setup,
                TestMatches.Bots(setup, "random", 5)).FinalState))
            .ToArray();
        MatchResult result = MatchRunner.Run(RulesConfig.Default, setup, TestMatches.Bots(setup, "random", 5));
        MatchRecord parsed = MatchRecord.FromJson(result.Record!.ToJson(), RulesConfig.Default);
        ReplayResult replay = MatchReplay.Run(RulesConfig.Default, parsed);

        Assert.Single(hashes.Distinct());
        Assert.Null(replay.DriftAtSeq);
        Assert.Equal(StateHash.Compute(result.FinalState), StateHash.Compute(replay.State));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void EveryListedOptionIsAcceptedWithFourSeats(int seed)
    {
        MatchSetup setup = seed % 2 == 0 ? TestMatches.Crossroads((ulong)seed, 4, 1, 2, 1, 2) : TestMatches.Crossroads((ulong)seed);
        List<string> failures = [];

        MatchRunner.Run(
            RulesConfig.Default,
            setup,
            TestMatches.Bots(setup, "random", seed),
            (state, _) => failures.AddRange(GameEngine.PendingDecisions(state)
                .SelectMany(decision => LegalActionsAgreementTests.CheckOptions(state, decision.Seat))));

        Assert.Empty(failures.Take(20));
    }

    /// <summary>Plays <paramref name="setup"/> with one <paramref name="bot"/> per seat, checking invariants.</summary>
    private static (MatchResult Result, List<string> Problems, List<GameEvent> Events) Play(
        MatchSetup setup,
        string bot,
        int seed)
    {
        List<string> problems = [];
        List<GameEvent> events = [];
        MatchResult result = MatchRunner.Run(
            RulesConfig.Default,
            setup,
            TestMatches.Bots(setup, bot, seed),
            (state, fired) =>
            {
                events.AddRange(fired);
                problems.AddRange(Invariants.Check(state).Select(problem => $"turn {state.Turn} {state.Phase}: {problem}"));
            },
            keepRecord: false);
        return (result, problems, events);
    }
}
