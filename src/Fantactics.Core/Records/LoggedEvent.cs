using Fantactics.Core.Events;

namespace Fantactics.Core.Records;

/// <summary>An event with the command that produced it and the turn it happened in.</summary>
/// <param name="Seq">Sequence number of the command.</param>
/// <param name="Turn">Turn the event happened in.</param>
/// <param name="Event">The event.</param>
public sealed record LoggedEvent(int Seq, int Turn, GameEvent Event)
{
    /// <summary>Appends command <paramref name="seq"/>'s events to <paramref name="log"/>, tracking the turn.</summary>
    /// <param name="log">The log to append to.</param>
    /// <param name="seq">The command's sequence number.</param>
    /// <param name="turn">The turn before the command.</param>
    /// <param name="events">The command's events.</param>
    public static void Append(List<LoggedEvent> log, int seq, int turn, IEnumerable<GameEvent> events)
    {
        foreach (GameEvent gameEvent in events)
        {
            turn = gameEvent is TurnStarted started ? started.Turn : turn;
            log.Add(new LoggedEvent(seq, turn, gameEvent));
        }
    }
}
