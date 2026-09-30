namespace Fantactics.Core.Events;

/// <summary>At the end of a turn, a player held objective tiles and scored (GameDesign §4.5).</summary>
/// <param name="Seat">The scoring player.</param>
/// <param name="Points">Points scored this turn.</param>
/// <param name="Total">The player's objective points after scoring.</param>
/// <param name="Held">Objective tiles the player held.</param>
/// <param name="EnemyHeld">The most objective tiles any enemy seat held.</param>
public sealed record ObjectiveScored(Seat Seat, int Points, int Total, int Held, int EnemyHeld) : GameEvent;
