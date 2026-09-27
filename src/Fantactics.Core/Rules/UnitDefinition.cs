using System.Collections.Immutable;

namespace Fantactics.Core.Rules;

/// <summary>A unit type's stats, traits, and abilities (RacesAndUnits §3.2, §4.2).</summary>
/// <param name="Race">Race identifier.</param>
/// <param name="Cost">Draft cost and deploy cost in Command.</param>
/// <param name="Hp">Maximum HP.</param>
/// <param name="Attack">Base damage of the basic attack.</param>
/// <param name="Defense">Flat damage reduction.</param>
/// <param name="Movement">Movement points per turn.</param>
/// <param name="MinRange">Minimum attack distance.</param>
/// <param name="MaxRange">Maximum attack distance.</param>
/// <param name="Initiative">Base initiative.</param>
/// <param name="Vision">Sight radius (unused until fog of war).</param>
/// <param name="Unique">At most one per army.</param>
/// <param name="Traits">Trait identifiers (see <see cref="TraitIds"/>) and their values.</param>
/// <param name="Abilities">Ability identifiers (see <see cref="AbilityIds"/>).</param>
public sealed record UnitDefinition(
    string Race,
    int Cost,
    int Hp,
    int Attack,
    int Defense,
    int Movement,
    int MinRange,
    int MaxRange,
    int Initiative,
    int Vision,
    bool Unique,
    ImmutableSortedDictionary<string, int> Traits,
    ImmutableArray<string> Abilities)
{
    /// <summary>Whether the unit's basic attack reaches beyond adjacent tiles.</summary>
    public bool IsRanged => MaxRange > 1;
}
