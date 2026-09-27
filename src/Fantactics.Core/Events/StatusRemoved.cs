using Fantactics.Core.State;

namespace Fantactics.Core.Events;

/// <summary>A status expired or was removed.</summary>
/// <param name="UnitId">The unit.</param>
/// <param name="Status">The status.</param>
public sealed record StatusRemoved(int UnitId, StatusKind Status) : GameEvent;
