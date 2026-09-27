namespace Fantactics.Core.State;

/// <summary>Status effects (RacesAndUnits §2.2).</summary>
public enum StatusKind
{
    /// <summary>−2 Movement, minimum 1.</summary>
    Slowed,

    /// <summary>Can't move, and gets no Held bonus.</summary>
    Rooted,
}
