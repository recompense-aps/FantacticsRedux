using System.Collections.Immutable;
using Fantactics.Core.Engine;
using Fantactics.Core.Maps;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Core.Records;

/// <summary>Everything needed to recreate a match's starting state.</summary>
/// <param name="Map">Built-in map name (see <see cref="MapLibrary"/>).</param>
/// <param name="Seed">Seed for rule randomness.</param>
/// <param name="Seats">Who plays each seat, e.g. <c>llm</c> or <c>bot:random</c>. Informational only.</param>
/// <param name="AllowedRaces">
/// Races each seat may draft from (GameDesign §4.4); a missing seat, or <c>null</c>, allows every race.
/// </param>
/// <param name="DraftBudgets">Per-seat draft budgets overriding the rules'; a missing seat, or <c>null</c>, uses it.</param>
/// <param name="StartingCaps">Per-seat starting caps overriding the rules'; a missing seat, or <c>null</c>, uses it.</param>
public sealed record MatchSetup(
    string Map,
    ulong Seed,
    ImmutableSortedDictionary<Seat, string> Seats,
    ImmutableSortedDictionary<Seat, ImmutableSortedSet<string>>? AllowedRaces = null,
    ImmutableSortedDictionary<Seat, int>? DraftBudgets = null,
    ImmutableSortedDictionary<Seat, int>? StartingCaps = null)
{
    /// <summary>
    /// Reads the one-race-per-seat setup of records made before the open draft (rules before 0.6.0) as
    /// <see cref="AllowedRaces"/>. Never written.
    /// </summary>
    public ImmutableSortedDictionary<Seat, string>? Races
    {
        get => null;
        init
        {
            if (value is not null)
            {
                AllowedRaces = value.ToImmutableSortedDictionary(
                    pair => pair.Key,
                    pair => ImmutableSortedSet.Create(pair.Value));
            }
        }
    }

    /// <summary>Creates the starting state.</summary>
    public GameState CreateInitialState(RulesConfig rules) =>
        GameEngine.NewMatch(rules, MapLibrary.Load(Map), Seed, AllowedRaces, DraftBudgets, StartingCaps);
}
