namespace Fantactics.Core.Engine;

/// <summary>The seat must place its starting army.</summary>
/// <param name="Seat">The seat.</param>
public sealed record PlaceStartingArmyDecision(Seat Seat) : Decision(Seat);
