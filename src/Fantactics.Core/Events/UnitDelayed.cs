namespace Fantactics.Core.Events;

/// <summary>A unit delayed to the end of the action order.</summary>
/// <param name="UnitId">The unit.</param>
public sealed record UnitDelayed(int UnitId) : GameEvent;
