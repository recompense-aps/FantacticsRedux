namespace Fantactics.Core.Engine;

/// <summary>The seat must choose the action for the unit whose initiative slot is up.</summary>
/// <param name="Seat">The unit's owner.</param>
/// <param name="UnitId">The acting unit.</param>
public sealed record ChooseUnitActionDecision(Seat Seat, int UnitId) : Decision(Seat);
