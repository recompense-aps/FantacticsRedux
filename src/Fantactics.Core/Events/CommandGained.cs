namespace Fantactics.Core.Events;

/// <summary>A player gained Command at the start of a turn.</summary>
/// <param name="Seat">The player.</param>
/// <param name="Amount">Command gained.</param>
/// <param name="Total">Command after the gain.</param>
public sealed record CommandGained(Seat Seat, int Amount, int Total) : GameEvent;
