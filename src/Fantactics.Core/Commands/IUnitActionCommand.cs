namespace Fantactics.Core.Commands;

/// <summary>An action-phase command for the unit whose initiative slot is up (GameDesign §4.2).</summary>
public interface IUnitActionCommand : ICommand
{
    /// <summary>The acting unit.</summary>
    int UnitId { get; }
}
