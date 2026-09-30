using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Events;
using Fantactics.Core.Players;
using Fantactics.Core.Records;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Core.Hosting;

/// <summary>
/// The push-based match wrapper that the server, local play, and the Sim CLI host (TechnicalDesign §2.4). Seats
/// submit commands in their own unit ids (<see cref="ViewIds"/>); after every accepted command each seat gets a
/// <see cref="SeatUpdate"/> through <see cref="Updated"/>. The host also plays a seat's queued actions when their
/// slot comes up, and, for seats with auto-skip on, makes units with nothing meaningful to do wait. It keeps the
/// match record and every event. Not thread-safe: callers serialize access.
/// </summary>
public sealed class MatchHost
{
    private readonly RulesConfig _rules;
    private readonly GameState? _customStart;
    private readonly List<RecordedCommand> _commands;
    private readonly List<LoggedEvent> _events;
    private readonly Dictionary<Seat, bool> _autoSkip = Enum.GetValues<Seat>().ToDictionary(seat => seat, _ => false);
    private readonly Dictionary<int, (Seat Seat, int Turn, ICommand Command)> _queued = [];
    private readonly List<GameEvent> _unpublished = [];

    /// <summary>Starts a new match.</summary>
    /// <param name="rules">Rules to play with.</param>
    /// <param name="setup">Map, seed, who plays each seat, and allowed races.</param>
    public MatchHost(RulesConfig rules, MatchSetup setup)
        : this(rules, setup, Fresh(rules, setup))
    {
    }

    private MatchHost(RulesConfig rules, MatchSetup setup, ResumePoint point)
    {
        _rules = rules;
        Setup = setup;
        State = point.State;
        _customStart = point.Custom ? point.Start : null;
        _commands = [.. point.Commands];
        _events = [.. point.Events];
        ResumeWarning = point.Warning;
    }

    /// <summary>Raised for each seat after every accepted submission (with any actions it set off), with what that seat may see.</summary>
    public event Action<Seat, SeatUpdate>? Updated;

    /// <summary>The match setup. Its seat labels change with <see cref="SetSeatLabel"/>.</summary>
    public MatchSetup Setup { get; private set; }

    /// <summary>The full current state. For the host only (persistence, tests, debug tools); never send it to a seat.</summary>
    public GameState State { get; private set; }

    /// <summary>Whether the match has ended.</summary>
    public bool IsOver => State.Outcome is not null;

    /// <summary>Why a resumed match's history was dropped (see <see cref="MatchResume"/>), or <c>null</c>.</summary>
    public string? ResumeWarning { get; }

    /// <summary>The accepted commands so far, in order.</summary>
    public IReadOnlyList<RecordedCommand> Commands => _commands;

    /// <summary>Every event so far, in engine ids. For the host only; seats get theirs through <see cref="Updated"/>.</summary>
    public IReadOnlyList<LoggedEvent> Events => _events;

    /// <summary>Continues a saved match from where <see cref="MatchResume"/> says it continues.</summary>
    /// <exception cref="MatchResumeException">The record doesn't replay and has no snapshot.</exception>
    public static MatchHost Resume(RulesConfig rules, MatchRecord record) =>
        new(rules, record.Setup, MatchResume.Resolve(rules, record));

    /// <summary>What <paramref name="seat"/> sees right now, with no new events (e.g. on connect or reconnect).</summary>
    public SeatUpdate Snapshot(Seat seat) =>
        new(PlayerView.Project(State, seat), [], LegalActions.ForView(State, seat));

    /// <summary>Submits a command from <paramref name="seat"/>, in the seat's own unit ids.</summary>
    /// <returns><c>null</c> if it was accepted, otherwise why not.</returns>
    public RuleViolation? Submit(Seat seat, ICommand command) =>
        SubmitEngine(seat, ViewIds.For(State, seat).ToEngine(command)) is Rejected rejected ? rejected.Violation : null;

    /// <summary>
    /// Submits a command already in engine ids (the Sim CLI, which names units by handle, and bots through
    /// <see cref="PlayerAgentExtensions.DecideFor"/>).
    /// </summary>
    /// <param name="seat">The submitting seat.</param>
    /// <param name="command">The command, in engine ids.</param>
    /// <param name="note">Optional reasoning to keep in the record.</param>
    public ApplyResult SubmitEngine(Seat seat, ICommand command, string? note = null)
    {
        ApplyResult result = Apply(seat, command, note);
        if (result is Accepted)
        {
            RunAutomatic();
        }

        Publish();
        return result;
    }

    /// <summary>
    /// Applies commands another process appended to this match's record (a shared match file), checking each one
    /// continues this record and reproduces its recorded state hash. Stops at the first that doesn't.
    /// </summary>
    /// <returns><c>true</c> if every command was applied; <c>false</c> if the records diverged.</returns>
    public bool CatchUp(IEnumerable<RecordedCommand> commands)
    {
        bool matched = true;
        foreach (RecordedCommand entry in commands)
        {
            ApplyResult result = GameEngine.Apply(State, entry.Seat, entry.Command);
            if (entry.Seq != _commands.Count + 1
                || result is not Accepted accepted
                || StateHash.Compute(accepted.State) != entry.StateHashAfter)
            {
                matched = false;
                break;
            }

            Commit(entry.Seat, entry.Command, entry.Note, accepted);
        }

        RunAutomatic();
        Publish();
        return matched;
    }

    /// <summary>
    /// Lets computer players make every decision they owe, one command at a time (each is published), until a seat
    /// without one owes a decision or the match ends. Each decision gets a fresh agent seeded by <see cref="BotSeeds"/>.
    /// </summary>
    /// <param name="agentFor">The agent for a seat and seed, or <c>null</c> if no bot plays that seat.</param>
    /// <exception cref="InvalidOperationException">A bot made an illegal move.</exception>
    public void PlayAgents(Func<Seat, int, IPlayerAgent?> agentFor)
    {
        while (NextAgentDecision(agentFor) is (Seat seat, IPlayerAgent agent))
        {
            if (SubmitEngine(seat, agent.DecideFor(State, seat)) is Rejected rejected)
            {
                throw new InvalidOperationException($"Bot {seat} made an illegal move: {rejected.Violation.Message}");
            }
        }
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

    /// <summary>
    /// Changes who <paramref name="seat"/> is labeled as played by in <see cref="MatchSetup.Seats"/> (e.g.
    /// <c>human</c>, <c>llm</c>, <c>bot:captain</c>). Replays ignore it; the Sim CLI reads it to know which seats it plays.
    /// </summary>
    public void SetSeatLabel(Seat seat, string label) =>
        Setup = Setup with { Seats = Setup.Seats.SetItem(seat, label) };

    /// <summary>The record of the match so far, with a snapshot of the current state; this is the save file.</summary>
    public MatchRecord ToRecord() => new(
        MatchRecord.CurrentFormatVersion,
        GameEngine.RulesVersion,
        _rules.Hash,
        Setup,
        [.. _commands],
        _customStart,
        State);

    private static ResumePoint Fresh(RulesConfig rules, MatchSetup setup)
    {
        GameState start = setup.CreateInitialState(rules);
        return new ResumePoint(start, false, [], [], start, null);
    }

    private (Seat Seat, IPlayerAgent Agent)? NextAgentDecision(Func<Seat, int, IPlayerAgent?> agentFor)
    {
        foreach (Decision decision in GameEngine.PendingDecisions(State))
        {
            if (agentFor(decision.Seat, BotSeeds.For(Setup.Seed, _commands.Count + 1, decision.Seat)) is IPlayerAgent agent)
            {
                return (decision.Seat, agent);
            }
        }

        return null;
    }

    private ApplyResult Apply(Seat seat, ICommand command, string? note)
    {
        ApplyResult result = GameEngine.Apply(State, seat, command);
        if (result is Accepted accepted)
        {
            Commit(seat, command, note, accepted);
        }

        return result;
    }

    private void Commit(Seat seat, ICommand command, string? note, Accepted accepted)
    {
        int seq = _commands.Count + 1;
        LoggedEvent.Append(_events, seq, State.Turn, accepted.Events);
        State = accepted.State;
        _commands.Add(new RecordedCommand(seq, seat, command, StateHash.Compute(State), note));
        _unpublished.AddRange(accepted.Events);
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

            Apply(decision.Seat, automatic, null);
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
        foreach (Seat seat in State.Seats)
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
