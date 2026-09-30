using System.Collections.Immutable;

namespace Fantactics.Core.Events;

/// <summary>
/// A routed seat left a match that goes on without it (GameDesign §4.5): all its units were removed, and it no
/// longer scores or decides anything.
/// </summary>
/// <param name="Seat">The eliminated seat.</param>
/// <param name="FieldUnits">Its units that were on the field (its reserve was removed too, unseen).</param>
public sealed record SeatEliminated(Seat Seat, ImmutableArray<int> FieldUnits) : GameEvent;
