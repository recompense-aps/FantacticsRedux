using System.Collections.Immutable;
using Fantactics.Core.Engine;
using Fantactics.Core.Events;

namespace Fantactics.Core.Hosting;

/// <summary>
/// What one seat learns after a change (decided 2026-09-28): the new view to draw, the events since the last update
/// to animate on the way there, and the seat's options if it owes a decision. Everything uses the seat's own unit
/// ids (<see cref="ViewIds"/>). A client never keeps its own game state; it draws <see cref="View"/>.
/// </summary>
/// <param name="View">The seat's view after the change.</param>
/// <param name="Events">What happened, in order, as the seat sees it.</param>
/// <param name="Legal">The seat's options, or <c>null</c> if it owes no decision.</param>
public sealed record SeatUpdate(PlayerView View, ImmutableArray<GameEvent> Events, LegalActions? Legal);
