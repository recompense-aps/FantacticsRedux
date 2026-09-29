namespace Fantactics.Core.Rules;

/// <summary>Trait identifiers used in <see cref="RulesConfig"/>.</summary>
public static class TraitIds
{
    /// <summary>Held unit gets the Braced initiative bonus when an enemy moves into its range.</summary>
    public const string Braced = "Braced";

    /// <summary>Strikes back once per turn at attackers from distance 1 that it survives.</summary>
    public const string Retaliate = "Retaliate";

    /// <summary>Heals the trait value after its attack deals damage.</summary>
    public const string Bloodthirst = "Bloodthirst";

    /// <summary>+1 Attack in clashes.</summary>
    public const string Reckless = "Reckless";

    /// <summary>Ignores the target's positive terrain Defense.</summary>
    public const string Crush = "Crush";

    /// <summary>Attacks apply Slowed for the trait value in turns.</summary>
    public const string Hamstring = "Hamstring";

    /// <summary>Aura: other friendly units of the same race within the trait value in tiles get +1 Bloodthirst.</summary>
    public const string WarCry = "WarCry";

    /// <summary>Forest costs 1 Movement.</summary>
    public const string Forestwalk = "Forestwalk";

    /// <summary>Forest doesn't block this unit's line of sight.</summary>
    public const string CanopySight = "CanopySight";

    /// <summary>Reserves may arrive on forest tiles with no enemy within 2.</summary>
    public const string FromTheTrees = "FromTheTrees";

    /// <summary>Mountains cost 2 Movement.</summary>
    public const string MountainBorn = "MountainBorn";

    /// <summary>Deploy discount for Cost 3+ units; may arrive on mountains in or next to the deploy zone.</summary>
    public const string OutOfTheCaves = "OutOfTheCaves";

    /// <summary>Moving from forest to adjacent forest costs 0.</summary>
    public const string ForestStride = "ForestStride";

    /// <summary>Avoids clashes by stopping on its previous tile.</summary>
    public const string Slippery = "Slippery";
}
