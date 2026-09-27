using System.Collections.Immutable;
using Fantactics.Core;
using Fantactics.Core.Records;

namespace Fantactics.Ai.Tests;

/// <summary>Shared setups for simulated matches.</summary>
internal static class TestMatches
{
    /// <summary>Elves (P1) vs Goblins (P2) on Riverford.</summary>
    public static MatchSetup Riverford(ulong seed) => new(
        "riverford",
        new Dictionary<Seat, string> { [Seat.P1] = "Elves", [Seat.P2] = "Goblins" }.ToImmutableSortedDictionary(),
        seed,
        new Dictionary<Seat, string> { [Seat.P1] = "bot:random", [Seat.P2] = "bot:random" }.ToImmutableSortedDictionary());

    /// <summary>Two random agents with seeds derived from <paramref name="seed"/>.</summary>
    public static IReadOnlyDictionary<Seat, IPlayerAgent> RandomAgents(int seed) => new Dictionary<Seat, IPlayerAgent>
    {
        [Seat.P1] = new RandomAgent(seed * 2),
        [Seat.P2] = new RandomAgent(seed * 2 + 1),
    };
}
