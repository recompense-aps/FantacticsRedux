namespace Fantactics.Core.Commands;

/// <summary>Does nothing this turn.</summary>
/// <param name="UnitId">The acting unit.</param>
public sealed record Wait(int UnitId) : IUnitActionCommand;
