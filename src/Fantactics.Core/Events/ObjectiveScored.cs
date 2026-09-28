namespace Fantactics.Core.Events;

/// <summary>At the end of a turn, a player held more objective tiles than the opponent and scored.</summary>
/// <param name="Seat">The scoring player.</param>
/// <param name="Points">Points scored this turn.</param>
/// <param name="Total">The player's objective points after scoring.</param>
/// <param name="Held">Objective tiles the player held.</param>
/// <param name="EnemyHeld">Objective tiles the opponent held.</param>
public sealed record ObjectiveScored(Seat Seat, int Points, int Total, int Held, int EnemyHeld) : GameEvent;
