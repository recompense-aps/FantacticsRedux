namespace Fantactics.Core.Rules;

/// <summary>Ability identifiers used in <see cref="RulesConfig"/>.</summary>
public static class AbilityIds
{
    /// <summary>Ranger: a basic attack that also Roots the target.</summary>
    public const string PinningShot = "PinningShot";

    /// <summary>Herbalist: heal an ally and remove Slowed and Rooted.</summary>
    public const string Mend = "Mend";

    /// <summary>Druid: turn a Plains, Road, or Hills tile into Forest.</summary>
    public const string Overgrowth = "Overgrowth";

    /// <summary>Druid: Root every enemy standing in Forest nearby.</summary>
    public const string Entangle = "Entangle";

    /// <summary>Mauler: Root a target at range without damage.</summary>
    public const string ThrowNet = "ThrowNet";

    /// <summary>Shaman: summon Grunts on adjacent tiles.</summary>
    public const string CallTheHorde = "CallTheHorde";
}
