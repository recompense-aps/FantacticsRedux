namespace Fantactics.Core.Rules;

/// <summary>How held objective tiles turn into objective points at the end of each turn (GameDesign §4.5).</summary>
public enum ObjectiveScoring
{
    /// <summary>The player holding more objective tiles scores the points; equal holdings score nothing.</summary>
    Majority,

    /// <summary>Each player scores the points for every objective tile they hold.</summary>
    PerTile,
}
