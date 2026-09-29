using System.Collections.Immutable;
using Fantactics.Ai.Profiles;
using Fantactics.Core;

namespace Fantactics.Sim.Tournaments;

/// <summary>What a tournament plays.</summary>
/// <param name="Map">Built-in map name.</param>
/// <param name="P1Races">Races P1 may draft, or <c>null</c> for any.</param>
/// <param name="P2Races">Races P2 may draft, or <c>null</c> for any.</param>
/// <param name="P1Bot">P1's bot name (a label only when <paramref name="P1Profile"/> is set).</param>
/// <param name="P2Bot">P2's bot name (a label only when <paramref name="P2Profile"/> is set).</param>
/// <param name="Games">Number of matches.</param>
/// <param name="Seed">Seed of the first match; match <c>i</c> uses <c>Seed + i</c>.</param>
/// <param name="Threads">Matches played at once; 0 uses every core. Results don't depend on it.</param>
/// <param name="P1Profile">A custom profile for P1 instead of a built-in bot, e.g. from a file.</param>
/// <param name="P2Profile">A custom profile for P2 instead of a built-in bot.</param>
/// <param name="DraftBudgets">Per-seat draft budgets overriding the rules', or <c>null</c>.</param>
/// <param name="StartingCaps">Per-seat starting caps overriding the rules', or <c>null</c>.</param>
public sealed record TournamentOptions(
    string Map,
    ImmutableSortedSet<string>? P1Races,
    ImmutableSortedSet<string>? P2Races,
    string P1Bot,
    string P2Bot,
    int Games,
    ulong Seed,
    int Threads,
    BotProfile? P1Profile = null,
    BotProfile? P2Profile = null,
    ImmutableSortedDictionary<Seat, int>? DraftBudgets = null,
    ImmutableSortedDictionary<Seat, int>? StartingCaps = null);
