namespace Fantactics.Core.State;

/// <summary>Where a match is in its flow (GameDesign §4.1, §4.4).</summary>
public enum Phase
{
    /// <summary>Both players secretly draft armies.</summary>
    Draft,

    /// <summary>Both players secretly place their starting armies.</summary>
    Placement,

    /// <summary>Both players secretly give move and deploy orders.</summary>
    Movement,

    /// <summary>Units act one at a time in initiative order.</summary>
    Action,

    /// <summary>The match has ended.</summary>
    Over,
}
