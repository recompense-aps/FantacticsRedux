namespace Fantactics.Core.Events;

/// <summary>A unit used its slot to wait.</summary>
/// <param name="UnitId">The unit.</param>
public sealed record UnitWaited(int UnitId) : GameEvent;
