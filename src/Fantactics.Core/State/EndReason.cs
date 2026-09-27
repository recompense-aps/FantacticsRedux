namespace Fantactics.Core.State;

/// <summary>Why a match ended (GameDesign §4.5).</summary>
public enum EndReason
{
    /// <summary>A player's army value fell below the rout threshold.</summary>
    Rout,

    /// <summary>The turn limit was reached.</summary>
    TurnLimit,
}
