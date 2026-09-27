namespace Fantactics.Core.Events;

/// <summary>A turn ended.</summary>
/// <param name="Turn">Turn number.</param>
public sealed record TurnEnded(int Turn) : GameEvent;
