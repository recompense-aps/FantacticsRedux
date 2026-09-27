namespace Fantactics.Core.Engine;

/// <summary>The seat must submit its hidden draft.</summary>
/// <param name="Seat">The seat.</param>
public sealed record DraftArmyDecision(Seat Seat) : Decision(Seat);
