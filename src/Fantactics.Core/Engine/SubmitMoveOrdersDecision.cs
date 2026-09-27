namespace Fantactics.Core.Engine;

/// <summary>The seat must submit its hidden move and deploy orders.</summary>
/// <param name="Seat">The seat.</param>
public sealed record SubmitMoveOrdersDecision(Seat Seat) : Decision(Seat);
