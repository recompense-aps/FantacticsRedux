using Fantactics.Ai;
using Fantactics.Core;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Events;
using Fantactics.Core.Records;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Sim.Matches;

/// <summary>
/// A match loaded from its record: the replayed state, the event history, and the bookkeeping for unit handles.
/// Submitting commands appends to the record, and bot seats are played automatically.
/// </summary>
public sealed class MatchSession
{
    private readonly RulesConfig _rules;
    private readonly List<RecordedCommand> _commands = [];
    private readonly List<LoggedEvent> _events = [];
    private readonly SortedDictionary<int, Seat> _owners = [];
    private readonly List<int> _fieldOrder = [];
    private readonly HashSet<int> _seenOnField = [];

    private MatchSession(RulesConfig rules, MatchSetup setup)
    {
        _rules = rules;
        Setup = setup;
        State = setup.CreateInitialState(rules);
    }

    /// <summary>The match setup.</summary>
    public MatchSetup Setup { get; }

    /// <summary>The current state.</summary>
    public GameState State { get; private set; }

    /// <summary>Every event so far, in order.</summary>
    public IReadOnlyList<LoggedEvent> Events => _events;

    /// <summary>Number of commands in the record.</summary>
    public int CommandCount => _commands.Count;

    /// <summary>Starts a new match.</summary>
    public static MatchSession Create(RulesConfig rules, MatchSetup setup) => new(rules, setup);

    /// <summary>Replays a record, checking every state hash.</summary>
    /// <exception cref="SimException">A command is rejected or a hash differs (the rules changed).</exception>
    public static MatchSession FromRecord(RulesConfig rules, MatchRecord record)
    {
        MatchSession session = new(rules, record.Setup);
        foreach (RecordedCommand entry in record.Commands)
        {
            if (session.Apply(entry.Seat, entry.Command) is Rejected rejected)
            {
                throw new SimException(
                    $"Command {entry.Seq} in the record is now rejected ({rejected.Violation.Message}). "
                    + $"The record was made with rules {record.RulesVersion}; these are {GameEngine.RulesVersion}.");
            }

            if (StateHash.Compute(session.State) != entry.StateHashAfter)
            {
                throw new SimException(
                    $"Rules drift at command {entry.Seq}: the replayed state differs from the record "
                    + $"(recorded with rules {record.RulesVersion}, now {GameEngine.RulesVersion}).");
            }

            session._commands.Add(entry);
        }

        return session;
    }

    /// <summary>Who plays <paramref name="seat"/>.</summary>
    public SeatKind KindOf(Seat seat) => new(Setup.Seats[seat]);

    /// <summary>The record for saving.</summary>
    public MatchRecord ToRecord() => new(
        MatchRecord.CurrentFormatVersion,
        GameEngine.RulesVersion,
        _rules.Hash,
        Setup,
        [.. _commands]);

    /// <summary>Applies a command from <paramref name="seat"/>; accepted commands are appended to the record.</summary>
    public ApplyResult Submit(Seat seat, ICommand command, string? note)
    {
        ApplyResult result = Apply(seat, command);
        if (result is Accepted)
        {
            _commands.Add(new RecordedCommand(_commands.Count + 1, seat, command, StateHash.Compute(State), note));
        }

        return result;
    }

    /// <summary>Plays every pending decision that belongs to a bot seat, until a non-bot seat owes one.</summary>
    /// <exception cref="SimException">A bot produced an illegal command.</exception>
    public void AdvanceBots()
    {
        while (GameEngine.PendingDecisions(State).FirstOrDefault(d => KindOf(d.Seat).IsBot) is Decision decision)
        {
            // Seeded from the match seed and position, so a record replays to the same bot choices.
            int seed = unchecked((int)(Setup.Seed ^ ((ulong)(_commands.Count + 1) * 0x9E3779B97F4A7C15UL)))
                ^ (int)decision.Seat;
            IPlayerAgent agent = BotFactory.Create(KindOf(decision.Seat).BotName ?? "", _rules, seed);
            LegalActions legal = LegalActions.For(State, decision.Seat)
                ?? throw new SimException($"No legal options for bot decision {decision}.");
            ICommand command = agent.Decide(PlayerView.Project(State, decision.Seat), decision, legal);
            if (Submit(decision.Seat, command, note: null) is Rejected rejected)
            {
                throw new SimException($"Bot {decision.Seat} made an illegal move: {rejected.Violation.Message}");
            }
        }
    }

    /// <summary>Sequence number of the seat's last command, or 0.</summary>
    public int LastSeqOf(Seat seat) => _commands.LastOrDefault(c => c.Seat == seat)?.Seq ?? 0;

    /// <summary>Unit handles from <paramref name="viewer"/>'s point of view.</summary>
    public UnitHandles HandlesFor(Seat viewer) => new(
        _owners.Where(pair => pair.Value == viewer).Select(pair => pair.Key),
        _fieldOrder.Where(id => _owners[id] != viewer));

    private ApplyResult Apply(Seat seat, ICommand command)
    {
        ApplyResult result = GameEngine.Apply(State, seat, command);
        if (result is not Accepted accepted)
        {
            return result;
        }

        int turn = State.Turn;
        int seq = _commands.Count + 1;
        foreach (GameEvent gameEvent in accepted.Events)
        {
            turn = gameEvent is TurnStarted started ? started.Turn : turn;
            _events.Add(new LoggedEvent(seq, turn, gameEvent));
            Track(gameEvent);
        }

        State = accepted.State;
        foreach (Unit unit in State.Units.Values)
        {
            _owners.TryAdd(unit.Id, unit.Owner);
        }

        return result;
    }

    /// <summary>Records owners and the order units first appeared on the field.</summary>
    private void Track(GameEvent gameEvent)
    {
        (int id, Seat owner)? appeared = gameEvent switch
        {
            UnitPlaced placed => (placed.UnitId, placed.Owner),
            UnitArrived arrived => (arrived.UnitId, arrived.Owner),
            UnitSummoned summoned => (summoned.UnitId, summoned.Owner),
            _ => null,
        };
        if (appeared is (int unitId, Seat unitOwner) && _seenOnField.Add(unitId))
        {
            _owners.TryAdd(unitId, unitOwner);
            _fieldOrder.Add(unitId);
        }
    }
}
