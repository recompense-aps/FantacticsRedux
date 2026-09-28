using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Events;
using Fantactics.Core.Records;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Core.Hosting;

/// <summary>
/// The push-based match wrapper that the server and local play host (TechnicalDesign §2.2, decided 2026-09-28).
/// Seats submit commands in their own unit ids (<see cref="ViewIds"/>); after every accepted command each seat gets
/// a <see cref="SeatUpdate"/> through <see cref="Updated"/>. The host also plays a seat's queued actions when their
/// slot comes up, and, for seats with auto-skip on, makes units with nothing meaningful to do wait. Not
/// thread-safe: callers serialize access.
/// </summary>
public sealed class MatchHost
{
    private readonly RulesConfig _rules;
    private readonly List<RecordedCommand> _commands = [];
    private readonly Dictionary<Seat, bool> _autoSkip = SeatExtensions.All.ToDictionary(seat => seat, _ => false);
    private readonly Dictionary<int, (Seat Seat, int Turn, ICommand Command)> _queued = [];
    private readonly List<GameEvent> _unpublished = [];

    /// <summary>Starts a new match.</summary>
    /// <param name="rules">Rules to play with.</param>
    /// <param name="setup">Map, races, seed, and who plays each seat.</param>
    public MatchHost(RulesConfig rules, MatchSetup setup)
    {
        _rules = rules;
        Setup = setup;
        State = setup.CreateInitialState(rules);
    }

    /// <summary>Raised for each seat after every accepted submission (with any actions it set off), with what that seat may see.</summary>
    public event Action<Seat, SeatUpdate>? Updated;

    /// <summary>The match setup.</summary>
    public MatchSetup Setup { get; }

    /// <summary>The full current state. For the host only (persistence, tests); never send it to a seat.</summary>
    public GameState State { get; private set; }

    /// <summary>Whether the match has ended.</summary>
    public bool IsOver => State.Outcome is not null;

    /// <summary>What <paramref name="seat"/> sees right now, with no new events (e.g. on connect or reconnect).</summary>
    public SeatUpdate Snapshot(Seat seat) =>
        new(PlayerView.Project(State, seat), [], LegalActions.ForView(State, seat));

    /// <summary>Submits a command from <paramref name="seat"/>, in the seat's own unit ids.</summary>
    /// <returns><c>null</c> if it was accepted, otherwise why not.</returns>
    public RuleViolation? Submit(Seat seat, ICommand command)
    {
        RuleViolation? violation = Apply(seat, ViewIds.For(State, seat).ToEngine(command));
        if (violation is null)
        {
            RunAutomatic();
        }

        Publish();
        return violation;
    }

    /// <summary>
    /// Queues an action for one of <paramref name="seat"/>'s units that hasn't acted this turn. It's played when the
    /// unit's slot comes up if it's still legal then (e.g. its target is still alive); otherwise the seat is asked.
    /// Queuing again for the same unit replaces the earlier action. Queued actions expire at the end of the turn.
    /// </summary>
    /// <returns><c>null</c> if it was queued (or played at once), otherwise why not.</returns>
    public RuleViolation? Queue(Seat seat, IUnitActionCommand command)
    {
        ICommand engine = ViewIds.For(State, seat).ToEngine(command);
        int unitId = ((IUnitActionCommand)engine).UnitId;
        bool waiting = State.Phase == Phase.Action
            && State.Units.TryGetValue(unitId, out Unit? unit)
            && unit.Owner == seat
            && (State.TurnState.ActionQueue.Contains(unitId) || State.TurnState.DelayedQueue.Contains(unitId));
        if (!waiting)
        {
            return new RuleViolation("cannot-queue", "Only your units that haven't acted yet this turn can queue actions.");
        }

        _queued[unitId] = (seat, State.Turn, engine);
        RunAutomatic();
        Publish();
        return null;
    }

    /// <summary>Removes a queued action, if any, for one of <paramref name="seat"/>'s units.</summary>
    public void ClearQueued(Seat seat, int unitId)
    {
        int engineId = ViewIds.For(State, seat).ToEngine(unitId);
        if (_queued.TryGetValue(engineId, out (Seat Seat, int Turn, ICommand Command) entry) && entry.Seat == seat)
        {
            _queued.Remove(engineId);
        }
    }

    /// <summary>
    /// Turns auto-skip on or off for <paramref name="seat"/>: with it on, units that have nothing meaningful to do
    /// (<see cref="ActionFilter.IsMeaningful"/>) wait without asking.
    /// </summary>
    public void SetAutoSkip(Seat seat, bool enabled)
    {
        _autoSkip[seat] = enabled;
        RunAutomatic();
        Publish();
    }

    /// <summary>The record of the match so far, replayable with <see cref="MatchReplay"/>.</summary>
    public MatchRecord ToRecord() => new(
        MatchRecord.CurrentFormatVersion,
        GameEngine.RulesVersion,
        _rules.Hash,
        Setup,
        [.. _commands]);

    private RuleViolation? Apply(Seat seat, ICommand command)
    {
        switch (GameEngine.Apply(State, seat, command))
        {
            case Accepted accepted:
                State = accepted.State;
                _commands.Add(new RecordedCommand(_commands.Count + 1, seat, command, StateHash.Compute(State), null));
                _unpublished.AddRange(accepted.Events);
                return null;
            case Rejected rejected:
                return rejected.Violation;
            default:
                throw new InvalidOperationException("Unknown apply result.");
        }
    }

    /// <summary>Plays queued actions and auto-skips until a seat has a real choice to make.</summary>
    private void RunAutomatic()
    {
        while (GameEngine.PendingDecisions(State) is [ChooseUnitActionDecision decision])
        {
            ICommand? automatic = null;
            if (_queued.Remove(decision.UnitId, out (Seat Seat, int Turn, ICommand Command) queued)
                && queued.Turn == State.Turn
                && GameEngine.Apply(State, decision.Seat, queued.Command) is Accepted)
            {
                automatic = queued.Command;
            }
            else if (_autoSkip[decision.Seat] && !ActionFilter.HasMeaningfulAction(State, decision.Seat))
            {
                automatic = new Wait(decision.UnitId);
            }

            if (automatic is null)
            {
                break;
            }

            Apply(decision.Seat, automatic);
        }

        foreach (int stale in _queued.Where(pair => pair.Value.Turn != State.Turn).Select(pair => pair.Key).ToList())
        {
            _queued.Remove(stale);
        }
    }

    /// <summary>
    /// Sends each seat one update with everything since the last one, so a command and the queued or skipped
    /// actions it set off arrive together (no prompt flashes up for a unit that is then skipped).
    /// </summary>
    private void Publish()
    {
        if (_unpublished.Count == 0)
        {
            return;
        }

        List<GameEvent> events = [.. _unpublished];
        _unpublished.Clear();
        foreach (Seat seat in SeatExtensions.All)
        {
            ViewIds ids = ViewIds.For(State, seat);
            SeatUpdate update = new(
                PlayerView.Project(State, seat),
                [.. events.Select(ids.ToView)],
                LegalActions.ForView(State, seat));
            Updated?.Invoke(seat, update);
        }
    }
}
