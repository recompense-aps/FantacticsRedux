namespace Fantactics.Core.Events;

/// <summary>A unit reached 0 HP and was removed.</summary>
/// <param name="UnitId">The unit.</param>
/// <param name="KillerId">The unit that dealt the final damage.</param>
/// <param name="Value">Army value it was worth (0 for summoned units).</param>
public sealed record UnitDied(int UnitId, int KillerId, int Value) : GameEvent;
