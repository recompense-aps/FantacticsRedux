namespace Fantactics.Core.Events;

/// <summary>A Slippery unit retreated instead of clashing (RacesAndUnits §3.2).</summary>
/// <param name="Tick">Movement tick.</param>
/// <param name="UnitId">The unit that retreated.</param>
/// <param name="EnemyId">The enemy it avoided.</param>
public sealed record ClashAvoided(int Tick, int UnitId, int EnemyId) : GameEvent;
