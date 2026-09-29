using System.Collections.Immutable;
using Fantactics.Ai.Profiles;
using Fantactics.Core;
using Fantactics.Core.Players;
using Fantactics.Core.Records;
using Fantactics.Core.Rules;

namespace Fantactics.Ai.Tests;

/// <summary>Shared setups for simulated matches.</summary>
internal static class TestMatches
{
    /// <summary>Elves (P1) vs Goblins (P2) on Riverford: each seat limited to one race.</summary>
    public static MatchSetup Riverford(ulong seed) => Riverford(seed, "Elves", "Goblins");

    /// <summary>A Riverford match with each seat limited to one race.</summary>
    public static MatchSetup Riverford(ulong seed, string p1Race, string p2Race) => Open(seed) with
    {
        AllowedRaces = new Dictionary<Seat, ImmutableSortedSet<string>>
        {
            [Seat.P1] = [p1Race],
            [Seat.P2] = [p2Race],
        }.ToImmutableSortedDictionary(),
    };

    /// <summary>Alternates by seed: odd seeds are Elves vs Goblins, even seeds use the open draft.</summary>
    public static MatchSetup Alternating(ulong seed) => seed % 2 == 0 ? Open(seed) : Riverford(seed);

    /// <summary>A Riverford match where both seats may draft every race (the open draft, GameDesign §4.4).</summary>
    public static MatchSetup Open(ulong seed) => new(
        "riverford",
        seed,
        new Dictionary<Seat, string> { [Seat.P1] = "bot", [Seat.P2] = "bot" }.ToImmutableSortedDictionary());

    /// <summary>The setup with new seat labels.</summary>
    public static MatchSetup WithSeats(this MatchSetup setup, string p1, string p2) => setup with
    {
        Seats = new Dictionary<Seat, string> { [Seat.P1] = p1, [Seat.P2] = p2 }.ToImmutableSortedDictionary(),
    };

    /// <summary>The bot factory hosts get: a bot from its spec and seed.</summary>
    public static IPlayerAgent CreateBot(string spec, int seed) => BotFactory.Create(spec, RulesConfig.Default, seed);

    /// <summary>Two random agents with seeds derived from <paramref name="seed"/>.</summary>
    public static IReadOnlyDictionary<Seat, IPlayerAgent> RandomAgents(int seed) => Bots("random", "random", seed);

    /// <summary>Two built-in bots with seeds derived from <paramref name="seed"/>.</summary>
    public static IReadOnlyDictionary<Seat, IPlayerAgent> Bots(string p1, string p2, int seed) =>
        new Dictionary<Seat, IPlayerAgent>
        {
            [Seat.P1] = BotLibrary.Create(p1, RulesConfig.Default, seed * 2),
            [Seat.P2] = BotLibrary.Create(p2, RulesConfig.Default, seed * 2 + 1),
        };
}
