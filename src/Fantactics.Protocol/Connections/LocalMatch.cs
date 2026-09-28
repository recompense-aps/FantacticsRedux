using Fantactics.Core;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Hosting;
using Fantactics.Core.Players;
using Fantactics.Core.Records;
using Fantactics.Core.Rules;

namespace Fantactics.Protocol.Connections;

/// <summary>
/// An in-process match for hotseat and vs-bot play: a <see cref="MatchHost"/>, bots for some seats, and a
/// connection per human seat. Work runs off the caller's thread one command at a time, so a slow bot never blocks
/// the UI; updates arrive on that worker thread (the Godot client marshals them to the main thread).
/// </summary>
public sealed class LocalMatch
{
    private readonly object _gate = new();
    private readonly MatchHost _host;
    private readonly IReadOnlyDictionary<Seat, IPlayerAgent> _bots;
    private readonly Dictionary<Seat, LocalGameConnection> _connections = [];

    /// <summary>Starts a match.</summary>
    /// <param name="rules">Rules to play with.</param>
    /// <param name="setup">Map, races, seed, and seat labels.</param>
    /// <param name="bots">Agents for the seats bots play; every other seat is human.</param>
    public LocalMatch(RulesConfig rules, MatchSetup setup, IReadOnlyDictionary<Seat, IPlayerAgent> bots)
    {
        _host = new MatchHost(rules, setup);
        _bots = bots;
        _host.Updated += (seat, update) =>
        {
            if (_connections.TryGetValue(seat, out LocalGameConnection? connection))
            {
                connection.Receive(update);
            }
        };
    }

    /// <summary>Whether the match has ended.</summary>
    public bool IsOver
    {
        get
        {
            lock (_gate)
            {
                return _host.IsOver;
            }
        }
    }

    /// <summary>The connection for a human seat (hotseat uses both).</summary>
    /// <exception cref="ArgumentException">A bot plays <paramref name="seat"/>.</exception>
    public IGameConnection Connect(Seat seat)
    {
        if (_bots.ContainsKey(seat))
        {
            throw new ArgumentException($"A bot plays {seat}.", nameof(seat));
        }

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

    /// <summary>The record of the match so far.</summary>
    public MatchRecord ToRecord()
    {
        lock (_gate)
        {
            return _host.ToRecord();
        }
    }

    internal Task<RuleViolation?> SubmitAsync(Seat seat, ICommand command) => Run(() => _host.Submit(seat, command));

    internal Task<RuleViolation?> QueueAsync(Seat seat, IUnitActionCommand command) =>
        Run(() => _host.Queue(seat, command));

    internal Task SetAutoSkipAsync(Seat seat, bool enabled) => Run(() =>
    {
        _host.SetAutoSkip(seat, enabled);
        return 0;
    });

    private Task<T> Run<T>(Func<T> action) => Task.Run(() =>
    {
        lock (_gate)
        {
            T result = action();
            PlayBots();
            return result;
        }
    });

    /// <summary>Plays bot decisions until a human owes one or the match ends.</summary>
    private void PlayBots()
    {
        while (GameEngine.PendingDecisions(_host.State).FirstOrDefault(d => _bots.ContainsKey(d.Seat)) is Decision decision)
        {
            SeatUpdate snapshot = _host.Snapshot(decision.Seat);
            LegalActions legal = snapshot.Legal
                ?? throw new InvalidOperationException($"{decision.Seat} owes a decision but has no options.");
            ICommand command = _bots[decision.Seat].Decide(snapshot.View, legal.Decision, legal);
            if (_host.Submit(decision.Seat, command) is RuleViolation violation)
            {
                throw new InvalidOperationException($"Bot {decision.Seat} made an illegal move: {violation.Message}");
            }
        }
    }
}
