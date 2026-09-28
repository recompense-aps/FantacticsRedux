namespace Fantactics.Core.Rules;

/// <summary>When a held unit with the Braced trait earns the Braced initiative bonus (GameDesign §4.1).</summary>
public enum BracedTrigger
{
    /// <summary>An enemy that moved this turn ended adjacent to it (the counter to a blind charge).</summary>
    Adjacent,

    /// <summary>An enemy that moved this turn ended anywhere from 1 tile to the unit's max range.</summary>
    InRange,
}
