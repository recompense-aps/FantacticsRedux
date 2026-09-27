namespace Fantactics.Core.Rules;

/// <summary>Tunable numbers for an ability. Its behavior is implemented in code, keyed by <see cref="AbilityIds"/>.</summary>
/// <param name="MinRange">Minimum target distance; <c>0</c> with <paramref name="MaxRange"/> <c>0</c> means untargeted.</param>
/// <param name="MaxRange">Maximum target distance, or the radius for area abilities.</param>
/// <param name="Cooldown">Turns the ability is unavailable after use.</param>
/// <param name="Amount">Heal amount, number of summons, etc.</param>
/// <param name="Duration">Turns a status it applies lasts.</param>
/// <param name="Limit">A cap, such as the number of summoned units alive at once.</param>
/// <param name="UsesAttackRange">Target within the unit's attack range instead of <paramref name="MinRange"/>–<paramref name="MaxRange"/>.</param>
/// <param name="NeedsLineOfSight">Whether the target must be in line of sight.</param>
public sealed record AbilityDefinition(
    int MinRange,
    int MaxRange,
    int Cooldown,
    int Amount,
    int Duration,
    int Limit,
    bool UsesAttackRange,
    bool NeedsLineOfSight);
