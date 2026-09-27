namespace Fantactics.Core.Commands;

/// <summary>Gives up the slot to act after every non-delayed unit; once per turn (GameDesign §4.2).</summary>
/// <param name="UnitId">The acting unit.</param>
public sealed record Delay(int UnitId) : IUnitActionCommand;
