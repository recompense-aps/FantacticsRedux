using System.Collections.Immutable;
using Fantactics.Core.Engine;
using Fantactics.Core.Maps;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Core.Records;

/// <summary>Everything needed to recreate a match's starting state.</summary>
/// <param name="Map">Built-in map name (see <see cref="MapLibrary"/>).</param>
/// <param name="Races">Race per seat.</param>
/// <param name="Seed">Seed for rule randomness.</param>
/// <param name="Seats">Who plays each seat, e.g. <c>llm</c> or <c>bot:random</c>. Informational only.</param>
public sealed record MatchSetup(
    string Map,
    ImmutableSortedDictionary<Seat, string> Races,
    ulong Seed,
    ImmutableSortedDictionary<Seat, string> Seats)
{
    /// <summary>Creates the starting state.</summary>
    public GameState CreateInitialState(RulesConfig rules) =>
        GameEngine.NewMatch(rules, MapLibrary.Load(Map), Races[Seat.P1], Races[Seat.P2], Seed);
}
