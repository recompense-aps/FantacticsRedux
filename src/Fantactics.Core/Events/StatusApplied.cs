using Fantactics.Core.State;

namespace Fantactics.Core.Events;

/// <summary>A status was applied or refreshed.</summary>
/// <param name="UnitId">The unit.</param>
/// <param name="Status">The status.</param>
/// <param name="LastsThroughTurn">The last turn the status is active.</param>
public sealed record StatusApplied(int UnitId, StatusKind Status, int LastsThroughTurn) : GameEvent;
