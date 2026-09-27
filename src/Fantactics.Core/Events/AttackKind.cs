namespace Fantactics.Core.Events;

/// <summary>What kind of strike dealt damage.</summary>
public enum AttackKind
{
    /// <summary>A basic attack in range.</summary>
    Basic,

    /// <summary>A ranged unit's half-Attack shot at an adjacent enemy.</summary>
    PointBlank,

    /// <summary>A strike during a clash.</summary>
    Clash,

    /// <summary>A Retaliate strike back.</summary>
    Retaliate,
}
