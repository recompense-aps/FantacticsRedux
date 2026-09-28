using Fantactics.Core.Events;

namespace Fantactics.Client.Logic.Playback;

/// <summary>One unit hits another.</summary>
/// <param name="AttackerId">The attacker.</param>
/// <param name="TargetId">The target.</param>
/// <param name="Kind">What kind of attack.</param>
/// <param name="Damage">Damage dealt.</param>
/// <param name="HpAfter">The target's HP afterwards.</param>
public sealed record StrikeStep(int AttackerId, int TargetId, AttackKind Kind, int Damage, int HpAfter) : Step;
