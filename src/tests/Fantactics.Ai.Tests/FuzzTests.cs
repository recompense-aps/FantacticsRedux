using System.Collections.Immutable;
using Fantactics.Core.Engine;
using Fantactics.Core.Rules;

namespace Fantactics.Ai.Tests;

/// <summary>Random vs random matches with invariant checks after every command (Simulation §7, layer 3).</summary>
public class FuzzTests
{
    public static IEnumerable<object[]> Seeds => Enumerable.Range(1, 100).Select(seed => new object[] { seed });

    [Theory]
    [MemberData(nameof(Seeds))]
    public void RandomMatchKeepsInvariantsAndEnds(int seed)
    {
        List<string> problems = [];

        MatchResult result = MatchRunner.Run(
            RulesConfig.Default,
            TestMatches.Alternating((ulong)seed),
            TestMatches.RandomAgents(seed),
            (state, _) =>
            {
                ImmutableArray<string> found = Invariants.Check(state);
                problems.AddRange(found.Select(problem => $"turn {state.Turn} {state.Phase}: {problem}"));
            });

        Assert.Empty(problems);
        Assert.InRange(result.Turns, 1, RulesConfig.Default.TurnLimit);
    }
}
