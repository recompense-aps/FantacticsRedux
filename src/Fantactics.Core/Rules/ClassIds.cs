namespace Fantactics.Core.Rules;

/// <summary>Class tags a unit type can carry alongside its race (RacesAndUnits §2.4).</summary>
public static class ClassIds
{
    /// <summary>Casters and terrain shapers; a race's signature units.</summary>
    public const string Mage = "Mage";

    /// <summary>Fights mainly from range.</summary>
    public const string Ranged = "Ranged";

    /// <summary>Animals and riders.</summary>
    public const string Mounted = "Mounted";

    /// <summary>Frontline anchors.</summary>
    public const string Defender = "Defender";
}
