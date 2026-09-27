using Fantactics.Core.Events;

namespace Fantactics.Sim.Matches;

/// <summary>An event with the command that produced it and the turn it happened in.</summary>
/// <param name="Seq">Sequence number of the command.</param>
/// <param name="Turn">Turn the event happened in.</param>
/// <param name="Event">The event.</param>
public sealed record LoggedEvent(int Seq, int Turn, GameEvent Event);
