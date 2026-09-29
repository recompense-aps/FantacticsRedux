namespace Fantactics.Core.Rules;

/// <summary>Which friendly units an ability that targets allies may affect (RacesAndUnits §2.4).</summary>
public enum AllyScope
{
    /// <summary>Only friendly units that share a race with the user (the default).</summary>
    OwnRace,

    /// <summary>Any friendly unit.</summary>
    AnyFriendly,
}
