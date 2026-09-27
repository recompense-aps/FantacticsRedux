using System.Collections.Immutable;

namespace Fantactics.Core.Rules;

/// <summary>A race's shared traits (RacesAndUnits §3.1, §4.1).</summary>
/// <param name="Traits">Trait identifiers (see <see cref="TraitIds"/>) every unit of the race has, with values.</param>
public sealed record RaceDefinition(ImmutableSortedDictionary<string, int> Traits);
