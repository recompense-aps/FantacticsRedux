namespace Fantactics.Core.State;

/// <summary>The result of a finished match.</summary>
/// <param name="Winner">The winning seat, or <c>null</c> for a draw.</param>
/// <param name="Reason">Why the match ended.</param>
public sealed record MatchOutcome(Seat? Winner, EndReason Reason);
