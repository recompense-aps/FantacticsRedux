using System.Runtime.CompilerServices;
using Fantactics.Core.Engine;
using Fantactics.Core.Hosting;
using Fantactics.Core.State;

namespace Fantactics.Client.Logic.Log;

/// <summary>
/// One seat's log of what happened, in that seat's unit ids, built from every update the seat receives. The screen
/// asks for the lines <see cref="Through"/> the update it's showing, so the log keeps step with playback. Appends
/// come on the match's worker thread and reads on the main thread, so both lock.
/// </summary>
public sealed class EventLog
{
    /// <summary>The most lines kept; older ones are dropped.</summary>
    public const int MaxLines = 500;

    private readonly object _lock = new();
    private readonly List<LogLine> _lines = [];
    private readonly UnitNames _names = new();
    private readonly ConditionalWeakTable<SeatUpdate, StrongBox<int>> _ends = [];
    private int _dropped;
    private Phase _phase;
    private int _turn;

    /// <summary>Starts a log from the seat's view before any updates (a new match, or one loaded mid-way).</summary>
    public EventLog(PlayerView view)
    {
        _names.Learn(view);
        _phase = view.Phase;
        _turn = view.Turn;
    }

    /// <summary>Adds an update's lines, once; appending the same update again does nothing.</summary>
    public void Append(SeatUpdate update)
    {
        lock (_lock)
        {
            if (_ends.TryGetValue(update, out _))
            {
                return;
            }

            _lines.AddRange(EventText.Lines(update.Events, _names, _turn));
            _names.Learn(update.View);
            if (update.View.Phase != _phase && EventText.PhaseLine(update.View.Phase) is string phaseLine)
            {
                _lines.Add(new LogLine(update.View.Turn, phaseLine, Heading: true));
            }

            _phase = update.View.Phase;
            _turn = update.View.Turn;
            if (_lines.Count > MaxLines)
            {
                int drop = _lines.Count - MaxLines;
                _lines.RemoveRange(0, drop);
                _dropped += drop;
            }

            _ends.Add(update, new StrongBox<int>(_dropped + _lines.Count));
        }
    }

    /// <summary>
    /// The lines up to and including <paramref name="update"/>'s, or every line for an update that was never
    /// appended (such as the one a seat started with).
    /// </summary>
    public IReadOnlyList<LogLine> Through(SeatUpdate update)
    {
        lock (_lock)
        {
            int end = _ends.TryGetValue(update, out StrongBox<int>? box)
                ? Math.Clamp(box.Value - _dropped, 0, _lines.Count)
                : _lines.Count;
            return _lines.GetRange(0, end);
        }
    }
}
