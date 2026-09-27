using Fantactics.Core.Geometry;

namespace Fantactics.Core.Events;

/// <summary>A unit used an ability. Its effects follow as separate events.</summary>
/// <param name="UnitId">The unit.</param>
/// <param name="Ability">Ability identifier.</param>
/// <param name="Target">Target tile, if any.</param>
public sealed record AbilityUsed(int UnitId, string Ability, Point? Target) : GameEvent;
