namespace Fantactics.Core.Events;

/// <summary>A unit regained HP.</summary>
/// <param name="UnitId">The unit.</param>
/// <param name="Amount">HP actually regained.</param>
/// <param name="HpAfter">HP after healing.</param>
public sealed record UnitHealed(int UnitId, int Amount, int HpAfter) : GameEvent;
