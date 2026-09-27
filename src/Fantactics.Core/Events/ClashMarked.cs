using Fantactics.Core.Geometry;

namespace Fantactics.Core.Events;

/// <summary>Two enemy units tried to take the same tile (or swap tiles) and will clash after movement.</summary>
/// <param name="Tick">Movement tick.</param>
/// <param name="UnitA">One unit.</param>
/// <param name="UnitB">The other unit.</param>
/// <param name="Tile">The contested tile; for a swap, <c>null</c> (the winner takes the loser's tile).</param>
public sealed record ClashMarked(int Tick, int UnitA, int UnitB, Point? Tile) : GameEvent;
