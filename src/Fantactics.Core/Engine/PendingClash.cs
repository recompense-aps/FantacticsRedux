using Fantactics.Core.Geometry;

namespace Fantactics.Core.Engine;

/// <summary>A clash marked during movement, resolved after all ticks.</summary>
/// <param name="Tick">Tick it was marked on.</param>
/// <param name="UnitA">One unit.</param>
/// <param name="UnitB">The other unit.</param>
/// <param name="Tile">The contested tile, or <c>null</c> for a swap (the winner takes the loser's tile).</param>
internal readonly record struct PendingClash(int Tick, int UnitA, int UnitB, Point? Tile);
