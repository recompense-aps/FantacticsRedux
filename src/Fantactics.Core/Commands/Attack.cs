namespace Fantactics.Core.Commands;

/// <summary>A basic attack (GameDesign §4.3).</summary>
/// <param name="UnitId">The attacker.</param>
/// <param name="TargetId">The enemy target.</param>
public sealed record Attack(int UnitId, int TargetId) : IUnitActionCommand;
