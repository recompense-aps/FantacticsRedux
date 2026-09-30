using Fantactics.Core;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Hosting;
using Fantactics.Core.Players;
using Fantactics.Core.Rules;
using Fantactics.Protocol.Connections;
using Fantactics.Protocol.Files;

namespace Fantactics.Client.Logic.Session;

/// <summary>
/// One match as the screen sees it: a connection per seat, which seat's view is shown, and the quick start, where a
/// bot drafts and places for human seats (skip the draft). In hotseat the shown seat follows whoever
/// owes a decision, and <see cref="ShownChanged"/> tells the screen to raise the curtain. Events are raised on the
/// match's worker thread; the Godot side marshals them to the main thread.
/// </summary>
public sealed class ClientSession
{
    private readonly Func<string, int, IPlayerAgent> _botFactory;
    private readonly string? _draftAs;
    private readonly Dictionary<Seat, IGameConnection> _connections;
    private readonly string? _autosave;
    private readonly object _switching = new();
    private int _shown;
    private int _savedTurn;

    /// <summary>Wraps a match.</summary>
    /// <param name="match">The match.</param>
    /// <param name="rules">Its rules.</param>
    /// <param name="botFactory">Creates bots from a spec and seed (for the quick start).</param>
    /// <param name="draftAs">Bot profile that drafts and places for human seats, or <c>null</c> to do it by hand.</param>
    /// <param name="shown">
    /// The seat to show first; defaults to the first human seat that owes a decision, or else the first human seat.
    /// </param>
    /// <param name="autosave">File to save the match to at the start of every turn, or <c>null</c>.</param>
    public ClientSession(
        LocalMatch match,
        RulesConfig rules,
        Func<string, int, IPlayerAgent> botFactory,
        string? draftAs,
        Seat? shown = null,
        string? autosave = null)
    {
        _autosave = autosave;
        _savedTurn = match.State.Turn;
        Match = match;
        Rules = rules;
        _botFactory = botFactory;
        _draftAs = draftAs;
        _connections = SeatExtensions.All.ToDictionary(seat => seat, match.Connect);
        Seat[] humans = [.. SeatExtensions.All.Where(IsHuman)];
        _shown = (int)(shown
            ?? humans.FirstOrDefault(seat => _connections[seat].Current.Legal is not null, humans.DefaultIfEmpty(Seat.P1).First()));
        foreach ((Seat seat, IGameConnection connection) in _connections)
        {
            connection.Updated += update => OnUpdated(seat, update);
        }
    }

    /// <summary>An update for the shown seat arrived (on the match's worker thread).</summary>
    public event Action<SeatUpdate>? Updated;

    /// <summary>The shown seat changed to another human seat (hotseat): hide the board until they're ready.</summary>
    public event Action<Seat>? ShownChanged;

    /// <summary>The match.</summary>
    public LocalMatch Match { get; }

    /// <summary>Its rules.</summary>
    public RulesConfig Rules { get; }

    /// <summary>The seat whose view is shown and who submits.</summary>
    public Seat Shown => (Seat)Volatile.Read(ref _shown);

    /// <summary>The shown seat's latest update.</summary>
    public SeatUpdate Current => _connections[Shown].Current;

    /// <summary>The shown seat's connection.</summary>
    public IGameConnection Connection => _connections[Shown];

    /// <summary>Whether a person at this machine plays <paramref name="seat"/>.</summary>
    public bool IsHuman(Seat seat) => Match.ControllerOf(seat).Kind == SeatControllerKind.Human;

    /// <summary>Lets bots move first if they owe decisions, then runs the quick start for human seats.</summary>
    public async Task StartAsync()
    {
        await Match.StartAsync();
        foreach ((Seat seat, IGameConnection connection) in _connections)
        {
            QuickStart(seat, connection.Current);
        }

        FollowDecisions();
    }

    /// <summary>
    /// What a bot would draft or place for the shown seat, to fill in the draft or placement screen in one click.
    /// </summary>
    /// <param name="profile">The bot profile, e.g. <c>captain</c>.</param>
    /// <param name="seed">The bot's seed; vary it for different suggestions.</param>
    /// <returns>The bot's command, or <c>null</c> when the seat doesn't owe a draft or placement.</returns>
    public ICommand? Suggest(string profile, int seed) =>
        Current is { Legal: { Decision: DraftArmyDecision or PlaceStartingArmyDecision } legal } update
            ? _botFactory(profile, seed).Decide(update.View, legal.Decision, legal)
            : null;

    /// <summary>Saves the match (record and snapshot) to <paramref name="path"/>.</summary>
    public void SaveTo(string path) => MatchFiles.Write(path, Match.ToRecord());

    /// <summary>Submits a command for the shown seat.</summary>
    public Task<RuleViolation?> SubmitAsync(ICommand command) => Connection.SubmitAsync(command);

    /// <summary>Queues an action for one of the shown seat's units that acts later this turn.</summary>
    public Task<RuleViolation?> QueueAsync(IUnitActionCommand command) => Connection.QueueAsync(command);

    /// <summary>Turns auto-skip on or off for every human seat.</summary>
    public async Task SetAutoSkipAsync(bool enabled)
    {
        foreach (Seat seat in SeatExtensions.All.Where(IsHuman))
        {
            await _connections[seat].SetAutoSkipAsync(enabled);
        }
    }

    private void OnUpdated(Seat seat, SeatUpdate update)
    {
        QuickStart(seat, update);
        if (_autosave is not null && update.View.Turn != _savedTurn)
        {
            _savedTurn = update.View.Turn;
            SaveTo(_autosave);
        }

        // Under the switching lock, so an update for the old seat can't slip out after the curtain for the new one.
        lock (_switching)
        {
            if (seat == Shown)
            {
                Updated?.Invoke(update);
            }

            FollowDecisions();
        }
    }

    /// <summary>
    /// Hotseat: when the shown seat has nothing to do and another human seat owes a decision, shows that seat instead,
    /// raising <see cref="ShownChanged"/> (for the curtain) and then passing on its latest update.
    /// </summary>
    private void FollowDecisions()
    {
        lock (_switching)
        {
            // Seats are updated one after another, so only switch once the shown seat's own update says it's done.
            Seat shown = Shown;
            Seat[] waiting = _connections[shown].Current.Legal is null
                ? [.. SeatExtensions.All.Where(other =>
                    other != shown && IsHuman(other) && _connections[other].Current.Legal is not null)]
                : [];
            if (waiting is [Seat next, ..])
            {
                Volatile.Write(ref _shown, (int)next);
                ShownChanged?.Invoke(next);
                Updated?.Invoke(_connections[next].Current);
            }
        }
    }

    /// <summary>Lets the quick-start bot answer a human seat's draft or placement.</summary>
    private void QuickStart(Seat seat, SeatUpdate update)
    {
        if (_draftAs is null
            || !IsHuman(seat)
            || update.Legal is not { Decision: DraftArmyDecision or PlaceStartingArmyDecision } legal)
        {
            return;
        }

        IPlayerAgent drafter = _botFactory(_draftAs, (int)seat + 1);
        _ = _connections[seat].SubmitAsync(drafter.Decide(update.View, legal.Decision, legal));
    }
}
