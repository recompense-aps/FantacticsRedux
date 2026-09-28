using Fantactics.Ai;
using Fantactics.Core;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Hosting;
using Fantactics.Core.Records;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Sim.Matches;

/// <summary>
/// A match loaded from its file, on the same <see cref="MatchHost"/> the client uses, so both make identical bot
/// moves on a shared file. Submitting commands appends to the record, and bot seats are played automatically.
/// </summary>
public sealed class MatchSession
{
    private readonly RulesConfig _rules;
    private readonly MatchHost _host;

    private MatchSession(RulesConfig rules, MatchHost host)
    {
        _rules = rules;
        _host = host;
    }

    /// <summary>The match setup.</summary>
    public MatchSetup Setup => _host.Setup;

    /// <summary>The current state.</summary>
    public GameState State => _host.State;

    /// <summary>Every event so far, in order.</summary>
    public IReadOnlyList<LoggedEvent> Events => _host.Events;

    /// <summary>Number of commands in the record.</summary>
    public int CommandCount => _host.Commands.Count;

    /// <summary>Why the history was dropped when the file was loaded (see <see cref="MatchResume"/>), or <c>null</c>.</summary>
    public string? ResumeWarning => _host.ResumeWarning;

    /// <summary>Starts a new match.</summary>
    public static MatchSession Create(RulesConfig rules, MatchSetup setup) => new(rules, new MatchHost(rules, setup));

    /// <summary>Continues a match from its record (see <see cref="MatchResume"/>).</summary>
    /// <exception cref="SimException">The record no longer replays and has no snapshot to continue from.</exception>
    public static MatchSession FromRecord(RulesConfig rules, MatchRecord record)
    {
        try
        {
            return new MatchSession(rules, MatchHost.Resume(rules, record));
        }
        catch (MatchResumeException ex)
        {
            throw new SimException(ex.Message);
        }
    }

    /// <summary>Who plays <paramref name="seat"/>.</summary>
    public SeatKind KindOf(Seat seat) => new(Setup.Seats[seat]);

    /// <summary>The record for saving, with a snapshot of the current state.</summary>
    public MatchRecord ToRecord() => _host.ToRecord();

    /// <summary>Applies a command from <paramref name="seat"/>; accepted commands are appended to the record.</summary>
    public ApplyResult Submit(Seat seat, ICommand command, string? note) => _host.SubmitEngine(seat, command, note);

    /// <summary>Plays every pending decision that belongs to a bot seat, until a non-bot seat owes one.</summary>
    /// <exception cref="SimException">A bot produced an illegal command.</exception>
    public void AdvanceBots()
    {
        try
        {
            _host.PlayAgents((seat, seed) =>
                KindOf(seat).BotName is string bot ? BotFactory.Create(bot, _rules, seed) : null);
        }
        catch (InvalidOperationException ex)
        {
            throw new SimException(ex.Message);
        }
    }

    /// <summary>Sequence number of the seat's last command, or 0.</summary>
    public int LastSeqOf(Seat seat) => _host.Commands.LastOrDefault(c => c.Seat == seat)?.Seq ?? 0;

    /// <summary>Unit handles from <paramref name="viewer"/>'s point of view.</summary>
    public UnitHandles HandlesFor(Seat viewer) => new(
        State.Owners.Where(pair => pair.Value == viewer).Select(pair => pair.Key),
        State.FieldOrder.Where(id => State.Owners[id] != viewer));
}
