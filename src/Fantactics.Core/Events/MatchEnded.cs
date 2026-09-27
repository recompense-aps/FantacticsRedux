using Fantactics.Core.State;

namespace Fantactics.Core.Events;

/// <summary>The match ended.</summary>
/// <param name="Winner">The winning seat, or <c>null</c> for a draw.</param>
/// <param name="Reason">Why it ended.</param>
public sealed record MatchEnded(Seat? Winner, EndReason Reason) : GameEvent;
