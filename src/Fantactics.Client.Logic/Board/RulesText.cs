using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Client.Logic.Board;

/// <summary>
/// One-line descriptions of abilities, traits, and statuses for players (RacesAndUnits §2.2, §3, §4), with numbers
/// taken from the rules where they're tunable.
/// </summary>
public static class RulesText
{
    /// <summary>Traits whose value is a number worth showing (heal amount, turns, radius).</summary>
    private static readonly HashSet<string> _valuedTraits = [TraitIds.Bloodthirst, TraitIds.Hamstring, TraitIds.WarCry];

    /// <summary>What an ability does, or empty for an ability without text.</summary>
    public static string Ability(string id, AbilityDefinition ability) => id switch
    {
        AbilityIds.PinningShot => $"A basic attack that also Roots the target for {Turns(ability.Duration)}.",
        AbilityIds.Mend => (ability.AllyScope == AllyScope.OwnRace ? "Heal another friendly unit of its race " : "Heal another friendly unit ")
            + $"{ability.Amount} HP and remove Slowed and Rooted.",
        AbilityIds.Overgrowth => "Turn a Plains, Road, or Hills tile into Forest, even an occupied one.",
        AbilityIds.Entangle =>
            $"Root every enemy standing in Forest within {ability.MaxRange} tiles for {Turns(ability.Duration)}.",
        AbilityIds.ThrowNet => $"Root a target for {Turns(ability.Duration)}. Deals no damage.",
        AbilityIds.CallTheHorde =>
            $"Summon {ability.Amount} Grunts on empty adjacent tiles; at most {ability.Limit} summoned alive at once.",
        _ => "",
    };

    /// <summary>
    /// An ability's targeting in words: <c>attack range</c>, <c>range 2–3</c>, <c>within 2</c>, or empty for an
    /// untargeted ability; with <c>, line of sight</c> when it needs one.
    /// </summary>
    public static string Range(AbilityDefinition ability)
    {
        string range = ability switch
        {
            { UsesAttackRange: true } => "attack range",
            { MaxRange: 0 } => "",
            { MinRange: 0 } => $"within {ability.MaxRange}",
            _ when ability.MinRange == ability.MaxRange => $"range {ability.MaxRange}",
            _ => $"range {ability.MinRange}–{ability.MaxRange}",
        };
        return ability.NeedsLineOfSight && range.Length > 0 ? $"{range}, line of sight" : range;
    }

    /// <summary>A trait's name, with its value when the value matters, e.g. <c>Bloodthirst 3</c>.</summary>
    public static string TraitTitle(string id, int value) =>
        _valuedTraits.Contains(id) ? $"{UnitText.Words(id)} {value}" : UnitText.Words(id);

    /// <summary>What a trait does at <paramref name="value"/>, or empty for a trait without text.</summary>
    public static string Trait(string id, int value) => id switch
    {
        TraitIds.Braced => "When it holds and an enemy moves next to it, it gets the larger Braced initiative bonus.",
        TraitIds.Retaliate => "Strikes back once per turn at a melee attacker it survives.",
        TraitIds.Bloodthirst => $"Heals {value} HP after its attack deals damage (not in clashes).",
        TraitIds.Reckless => "+1 Attack in clashes.",
        TraitIds.Crush => "Ignores the target's terrain Defense bonus.",
        TraitIds.Hamstring => $"Its attacks (not clash strikes) Slow the target for {Turns(value)}.",
        TraitIds.WarCry => $"Aura: other friendly units of its race within {value} tiles get +1 Bloodthirst.",
        TraitIds.Forestwalk => "Forest costs 1 Movement.",
        TraitIds.CanopySight => "Forest doesn't block its line of sight.",
        TraitIds.FromTheTrees => "Reserves may arrive on forest tiles with no enemy within 2.",
        TraitIds.MountainBorn => "Mountains cost 2 Movement.",
        TraitIds.OutOfTheCaves =>
            "Reserves of Cost 3+ deploy for 1 less Command and may arrive on mountains in or next to the deploy zone.",
        TraitIds.ForestStride => "Moving from forest to an adjacent forest tile costs 0 Movement.",
        TraitIds.Slippery => "Avoids clashes by stopping on its previous tile.",
        _ => "",
    };

    /// <summary>What a status does.</summary>
    public static string Status(StatusKind status) => status switch
    {
        StatusKind.Slowed => "−2 Movement (minimum 1).",
        StatusKind.Rooted => "Can't move and gets no Held bonus. It can still act.",
        _ => "",
    };

    private static string Turns(int count) => count == 1 ? "1 turn" : $"{count} turns";
}
