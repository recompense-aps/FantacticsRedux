namespace Fantactics.Core.Engine;

/// <summary>The exact outcome of an attack, shown before it's confirmed (GameDesign §4.3).</summary>
/// <param name="TargetId">The target.</param>
/// <param name="Damage">Damage it would deal.</param>
/// <param name="Kills">Whether the target would die.</param>
public sealed record AttackPreview(int TargetId, int Damage, bool Kills);
