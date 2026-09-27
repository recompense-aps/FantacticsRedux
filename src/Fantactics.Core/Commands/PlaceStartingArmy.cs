using System.Collections.Immutable;

namespace Fantactics.Core.Commands;

/// <summary>A player's hidden starting placement (GameDesign §4.4). Every unplaced unit must be placed.</summary>
/// <param name="Placements">One placement per starting unit.</param>
public sealed record PlaceStartingArmy(ImmutableArray<UnitPlacement> Placements) : ICommand;
