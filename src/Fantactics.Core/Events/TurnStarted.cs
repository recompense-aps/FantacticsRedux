namespace Fantactics.Core.Events;

/// <summary>A new turn began.</summary>
/// <param name="Turn">Turn number, starting at 1.</param>
/// <param name="TiePriority">Seat that wins initiative ties this turn.</param>
public sealed record TurnStarted(int Turn, Seat TiePriority) : GameEvent;
