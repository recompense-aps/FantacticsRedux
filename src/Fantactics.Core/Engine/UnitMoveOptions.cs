using System.Collections.Immutable;

namespace Fantactics.Core.Engine;

/// <summary>Where one unit can move this turn. An empty list means it must hold.</summary>
/// <param name="UnitId">The unit.</param>
/// <param name="Destinations">Reachable tiles with their cheapest paths.</param>
public sealed record UnitMoveOptions(int UnitId, ImmutableArray<ReachableTile> Destinations);
