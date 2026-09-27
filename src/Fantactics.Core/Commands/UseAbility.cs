using Fantactics.Core.Geometry;

namespace Fantactics.Core.Commands;

/// <summary>Uses one of the unit's abilities.</summary>
/// <param name="UnitId">The acting unit.</param>
/// <param name="Ability">Ability identifier.</param>
/// <param name="Target">Target tile, or <c>null</c> for untargeted abilities.</param>
public sealed record UseAbility(int UnitId, string Ability, Point? Target) : IUnitActionCommand;
