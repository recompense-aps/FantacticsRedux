namespace Fantactics.Ai.Profiles;

/// <summary>
/// How well a bot plays: its thinking budget and the human-like mistakes it makes. Difficulty presets are
/// <c>Data/difficulty-*.json</c>; a profile can override any field.
/// </summary>
public sealed record SkillSettings
{
    /// <summary>Work allowed per decision.</summary>
    public ThinkBudget Budget { get; init; } = new();

    /// <summary>Softmax temperature over scored options, in evaluation points; 0 always picks the best.</summary>
    public double Temperature { get; init; }

    /// <summary>Chance to pick from the worse half of the options instead (never an illegal one).</summary>
    public double BlunderChance { get; init; }

    /// <summary>
    /// Blind spot: enemies farther than this from a unit are ignored when judging it; <c>null</c> sees all.
    /// </summary>
    public int? VisionRange { get; init; }

    /// <summary>Blind spot: ignores where enemies can strike next turn.</summary>
    public bool IgnoreThreats { get; init; }
}
