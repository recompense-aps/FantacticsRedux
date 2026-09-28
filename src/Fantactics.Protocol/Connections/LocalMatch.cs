using Fantactics.Core;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Hosting;
using Fantactics.Core.Players;
using Fantactics.Core.Records;
using Fantactics.Core.State;

namespace Fantactics.Protocol.Connections;

/// <summary>
/// An in-process match for hotseat, vs-bot, and shared-file play: a <see cref="MatchHost"/>, a controller per seat
/// (from the record's seat labels, see <see cref="SeatController"/>), and a connection per seat. Work runs off the
/// caller's thread one operation at a time, so a slow bot never blocks the UI; updates arrive on that worker thread
/// (the Godot client marshals them to the main thread). Bots get a fresh agent per decision
/// (<see cref="MatchHost.PlayAgents"/>), so a record plays out the same here as in the Sim CLI.
/// </summary>
public sealed class LocalMatch
{
    private readonly object _gate = new();
    private readonly MatchHost _host;
    private readonly Func<string, int, IPlayerAgent> _botFactory;
    private readonly IMatchSync? _sync;
    private readonly Dictionary<Seat, LocalGameConnection> _connections = [];

    /// <summary>Hosts a match.</summary>
    /// <param name="host">The match, new (<see cref="MatchHost(Core.Rules.RulesConfig, MatchSetup)"/>) or resumed.</param>
    /// <param name="botFactory">Creates a bot from its spec and a seed (the client passes the Ai bot factory).</param>
    /// <param name="sync">Storage other processes share, if any (see <see cref="IMatchSync"/>).</param>
    public LocalMatch(MatchHost host, Func<string, int, IPlayerAgent> botFactory, IMatchSync? sync = null)
    {
        _host = host;
        _botFactory = botFactory;
        _sync = sync;
        _host.Updated += (seat, update) =>
        {
            if (_connections.TryGetValue(seat, out LocalGameConnection? connection))
            {
                connection.Receive(update);
            }
        };
    }

    /// <summary>Whether the match has ended.</summary>
    public bool IsOver => Read(host => host.IsOver);

    /// <summary>The full current state, for debug tools (god view). Never show it to a player.</summary>
    public GameState State => Read(host => host.State);

    /// <summary>Every event so far, in engine ids, for debug tools.</summary>
    public IReadOnlyList<LoggedEvent> Events => Read(host => host.Events.ToList());

    /// <summary>Why a resumed match's history was dropped, or <c>null</c>.</summary>
    public string? ResumeWarning => _host.ResumeWarning;

    /// <summary>Who controls <paramref name="seat"/>.</summary>
    public SeatController ControllerOf(Seat seat) => Read(host => SeatController.Parse(host.Setup.Seats[seat]));

    /// <summary>The connection for <paramref name="seat"/>. Submissions only matter while a human controls it.</summary>
    public IGameConnection Connect(Seat seat)
    {
        lock (_gate)
        {
            if (!_connections.TryGetValue(seat, out LocalGameConnection? connection))
            {
                connection = new LocalGameConnection(this, seat, _host.Snapshot(seat));
                _connections[seat] = connection;
            }

            return connection;
        }
    }

    /// <summary>Lets the bots make any decisions they owe (e.g. both seats are bots, or a bot drafts first).</summary>
    public Task StartAsync() => Run(() => 0);

    /// <summary>Picks up changes other processes made to shared storage, and lets bots answer them.</summary>
    public Task RefreshAsync() => Run(() => 0);

    /// <summary>Hands <paramref name="seat"/> to another controller mid-match; a bot takes over at once.</summary>
    public Task SetControllerAsync(Seat seat, SeatController controller) => Run(() =>
    {
        _host.SetSeatLabel(seat, controller.Label);
        return 0;
    });

    /// <summary>The record of the match so far, with a snapshot of the current state.</summary>
    public MatchRecord ToRecord() => Read(host => host.ToRecord());

    /// <summary>A new match continuing from after command <paramref name="seq"/> of this one (rewind and branch).</summary>
    public LocalMatch Branch(int seq) =>
        new(MatchHost.Resume(Read(host => host.State.Rules), ToRecord().Truncated(seq)), _botFactory);

    internal Task<RuleViolation?> SubmitAsync(Seat seat, ICommand command) => Run(() => _host.Submit(seat, command));

    internal Task<RuleViolation?> QueueAsync(Seat seat, IUnitActionCommand command) =>
        Run(() => _host.Queue(seat, command));

    internal Task SetAutoSkipAsync(Seat seat, bool enabled) => Run(() =>
    {
        _host.SetAutoSkip(seat, enabled);
        return 0;
    });

    private T Read<T>(Func<MatchHost, T> read)
    {
        lock (_gate)
        {
            return read(_host);
        }
    }

    private Task<T> Run<T>(Func<T> action) => Task.Run(() =>
    {
        lock (_gate)
        {
            using IDisposable? sync = _sync?.Enter(_host);
            T result = action();
            _host.PlayAgents(AgentFor);
            return result;
        }
    });

    private IPlayerAgent? AgentFor(Seat seat, int seed) =>
        SeatController.Parse(_host.Setup.Seats[seat]) is { Kind: SeatControllerKind.Bot, BotSpec: string spec }
            ? _botFactory(spec, seed)
            : null;
}
