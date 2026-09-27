namespace Fantactics.Core.Events;

/// <summary>Why a unit stopped before the end of its path.</summary>
public enum StopReason
{
    /// <summary>It became adjacent to an enemy.</summary>
    ZoneOfControl,

    /// <summary>Its next tile was occupied by an enemy, contested, or claimed by a faster friendly unit.</summary>
    Blocked,

    /// <summary>It had to stop on a friendly unit's tile and backed up to the last free tile of its path.</summary>
    BackedUp,

    /// <summary>It is about to clash.</summary>
    Clash,
}
