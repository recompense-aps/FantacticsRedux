namespace Fantactics.Ai.Profiles;

/// <summary>
/// How a bot likes to play: the weights of its evaluation features (see <see cref="Evaluation.Features"/>) and its
/// leanings. Aggressive styles weight <see cref="Opportunity"/> and <see cref="Advance"/>; defensive styles weight
/// <see cref="Exposure"/>, <see cref="Terrain"/>, and <see cref="Hold"/>.
/// </summary>
public sealed record StyleWeights
{
    /// <summary>Army value, with units on the field scaled by their remaining HP.</summary>
    public double Material { get; init; } = 1.0;

    /// <summary>Turn-limit score difference, scaled up as the turn limit nears.</summary>
    public double Score { get; init; } = 0.5;

    /// <summary>Objective tiles held beyond the opponent's.</summary>
    public double Objectives { get; init; } = 1.5;

    /// <summary>Value the enemy could destroy next turn (subtracted).</summary>
    public double Exposure { get; init; } = 0.5;

    /// <summary>Value units could destroy next turn from where they stand.</summary>
    public double Opportunity { get; init; } = 0.4;

    /// <summary>Terrain Defense under units.</summary>
    public double Terrain { get; init; } = 0.3;

    /// <summary>Closeness to the enemy and the objectives.</summary>
    public double Advance { get; init; } = 0.3;

    /// <summary>Friendly neighbors (Support and mutual cover).</summary>
    public double Cohesion { get; init; } = 0.15;

    /// <summary>Enemy units Rooted or Slowed, minus own.</summary>
    public double Disable { get; init; } = 0.5;

    /// <summary>Standing still for the Held (and Braced) initiative bonus.</summary>
    public double Hold { get; init; } = 0.1;

    /// <summary>
    /// 0–1: how readily reserves are deployed; below 0.5 the bot banks Command for its biggest reserve.
    /// </summary>
    public double ReserveEagerness { get; init; } = 0.8;

    /// <summary>
    /// 0–1: units below this fraction of their HP pull back: their exposure counts triple and they stop advancing.
    /// </summary>
    public double RetreatThreshold { get; init; }

    /// <summary>Bonus for attacking already wounded enemies, per point of the target's missing value.</summary>
    public double FocusFire { get; init; }

    /// <summary>Flat bonus for any attack over waiting; a reckless bot hits whatever is in range.</summary>
    public double Bloodlust { get; init; }

    /// <summary>Enemy unit types worth more (or less) than their Cost, e.g. <c>{ "Shaman": 1.5 }</c>.</summary>
    public IReadOnlyDictionary<string, double> Priorities { get; init; } = new Dictionary<string, double>();

    /// <summary>
    /// Movement stances the bot considers (see <see cref="Planning.Stance"/>) and a bonus for choosing each. Empty
    /// considers every stance with no bonus.
    /// </summary>
    public IReadOnlyDictionary<string, double> Stances { get; init; } = new Dictionary<string, double>();

    /// <summary>Softmax temperature when picking a stance; above 0 makes the bot's plans hard to predict.</summary>
    public double StanceTemperature { get; init; }

    /// <summary>How the starting army is placed.</summary>
    public Formation Formation { get; init; } = Formation.Block;

    /// <summary>Multipliers on how much the draft wants each unit type, e.g. <c>{ "Tank": 1.5 }</c>.</summary>
    public IReadOnlyDictionary<string, double> DraftBias { get; init; } = new Dictionary<string, double>();

    /// <summary>
    /// Softmax temperature for draft picks; 0 uses the skill's temperature. High values draft at random.
    /// </summary>
    public double DraftTemperature { get; init; }

    /// <summary>
    /// How much the draft builds around one race (RacesAndUnits §2.4): each pick's score is multiplied by
    /// <c>1 + RaceFocus × the share of units picked so far from its race</c>. 0 ignores race.
    /// </summary>
    public double RaceFocus { get; init; }
}
