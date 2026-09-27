namespace Fantactics.Core.Events;

/// <summary>A unit dealt damage to another.</summary>
/// <param name="AttackerId">The attacker.</param>
/// <param name="TargetId">The target.</param>
/// <param name="Kind">What kind of strike it was.</param>
/// <param name="Damage">Damage dealt.</param>
/// <param name="HpAfter">Target HP after the strike.</param>
public sealed record UnitAttacked(int AttackerId, int TargetId, AttackKind Kind, int Damage, int HpAfter) : GameEvent;
