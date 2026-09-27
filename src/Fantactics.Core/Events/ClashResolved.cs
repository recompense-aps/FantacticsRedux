namespace Fantactics.Core.Events;

/// <summary>A clash finished.</summary>
/// <param name="UnitA">One unit.</param>
/// <param name="UnitB">The other unit.</param>
/// <param name="WinnerId">The survivor, or <c>null</c> if the strike cap was reached with both alive.</param>
public sealed record ClashResolved(int UnitA, int UnitB, int? WinnerId) : GameEvent;
