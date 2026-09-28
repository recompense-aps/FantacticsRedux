using Fantactics.Core.Engine;
using Fantactics.Core.Records;
using Fantactics.Core.Rules;

namespace Fantactics.Ai.Tests;

/// <summary>Same seed, same match; and records replay exactly (Simulation §7, layers 4–5).</summary>
public class DeterminismTests
{
    [Fact]
    public void SameSeedsProduceIdenticalMatchesEvenInParallel()
    {
        string[] hashes = Enumerable.Range(0, 4)
            .AsParallel()
            .Select(_ => StateHash.Compute(Play(7).FinalState))
            .ToArray();

        Assert.Single(hashes.Distinct());
    }

    [Fact]
    public void DifferentSeedsProduceDifferentMatches()
    {
        Assert.NotEqual(StateHash.Compute(Play(7).FinalState), StateHash.Compute(Play(8).FinalState));
    }

    [Fact]
    public void RecordRoundTripsThroughJsonAndReplaysWithoutDrift()
    {
        MatchResult result = Play(11);

        MatchRecord record = result.Record ?? throw new InvalidOperationException("The runner kept no record.");
        MatchRecord parsed = MatchRecord.FromJson(record.ToJson(), RulesConfig.Default);
        ReplayResult replay = MatchReplay.Run(RulesConfig.Default, parsed);

        Assert.Null(replay.DriftAtSeq);
        Assert.Equal(StateHash.Compute(result.FinalState), StateHash.Compute(replay.State));
        Assert.Equal(record.Commands.Length, parsed.Commands.Length);
    }

    private static MatchResult Play(int seed) =>
        MatchRunner.Run(RulesConfig.Default, TestMatches.Riverford((ulong)seed), TestMatches.RandomAgents(seed));
}
