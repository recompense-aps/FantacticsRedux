namespace Fantactics.Ai.Planning;

/// <summary>
/// A plan for one movement phase: a reweighting of the bot's style used to build one candidate set of orders.
/// The move planner builds one candidate per stance and keeps the one whose simulated outcome scores best.
/// </summary>
public enum Stance
{
    /// <summary>The style as it is.</summary>
    Balanced,

    /// <summary>Charge: advance and strike, ignoring most danger.</summary>
    AllIn,

    /// <summary>Stay put on good ground for the Held and Braced bonuses.</summary>
    HoldLine,

    /// <summary>Take and hold the objective tiles.</summary>
    ObjectivePush,

    /// <summary>Pull back out of reach, onto cover.</summary>
    FallBack,

    /// <summary>Close in to just outside the enemy's reach, inviting a bad charge.</summary>
    Bait,
}
