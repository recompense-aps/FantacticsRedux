namespace Fantactics.Core.State;

/// <summary>Where a living unit is.</summary>
public enum UnitLocation
{
    /// <summary>Drafted into the starting army but not placed yet.</summary>
    Unplaced,

    /// <summary>Waiting off the map to be deployed with Command.</summary>
    Reserve,

    /// <summary>On the map.</summary>
    Field,
}
