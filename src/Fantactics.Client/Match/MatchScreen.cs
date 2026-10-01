using System.Globalization;
using Fantactics.Client.Common;
using Fantactics.Client.Debug;
using Fantactics.Client.Logic.Board;
using Fantactics.Client.Logic.Debug;
using Fantactics.Client.Logic.Input;
using Fantactics.Client.Logic.Playback;
using Fantactics.Client.Logic.Session;
using Fantactics.Client.Logic.Settings;
using Fantactics.Client.Match.Board;
using Fantactics.Client.Match.Curtain;
using Fantactics.Client.Match.Draft;
using Fantactics.Client.Match.Hud;
using Fantactics.Client.Match.Menu;
using Fantactics.Client.Match.Playback;
using Fantactics.Core;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Geometry;
using Fantactics.Core.Hosting;
using Fantactics.Core.Records;
using Fantactics.Core.State;
using Fantactics.Protocol.Connections;
using Godot;

namespace Fantactics.Client.Match;

/// <summary>
/// One match on screen. Updates from the session are queued and played in order: the event player animates each
/// one, then the board and HUD snap to its view. In hotseat, a curtain goes up in that queue whenever the shown seat
/// changes, so nothing of the next seat's view appears until they're ready. Clicks and keys go to the builder for the
/// decision the shown seat owes (draft, placement, move orders, or an action), and finished commands are submitted
/// through the session.
/// </summary>
public partial class MatchScreen : Node
{
    /// <summary>Screen pixels the HUD takes at the top (status and prompt lines).</summary>
    private const float HudTop = 64;

    /// <summary>Screen pixels the HUD takes at the bottom (buttons).</summary>
    private const float HudBottom = 52;

    /// <summary>The bot profile behind "Bot pick" and "Auto-place".</summary>
    private const string SuggestProfile = "captain";

    private static readonly double[] _speeds = [1, 2, 4, 0];

    private readonly Queue<Queued> _pending = new();
    private OpenMatch _open = null!;
    private ClientSession _session = null!;
    private SaveLocations _saves = null!;
    private IReadOnlyList<string> _seatOptions = [];
    private ClientSettings _settings = new();
    private Action<ClientSettings> _saveSettings = _ => { };
    private SeatUpdate? _current;
    private DecisionInput? _input;
    private bool _pumping;
    private bool _finished;
    private DateTime? _llmSince;
    private int _llmSeconds = -1;
    private bool _submitting;

    [Export]
    private BoardView _board = null!;

    [Export]
    private Camera2D _camera = null!;

    [Export]
    private EventPlayer _player = null!;

    [Export]
    private MatchHud _hud = null!;

    [Export]
    private DraftPanel _draftPanel = null!;

    [Export]
    private DebugPanel _debug = null!;

    [Export]
    private MatchMenu _menu = null!;

    [Export]
    private HotseatCurtain _curtain = null!;

    /// <summary>The match ended and its last update has played; <paramref name="result"/> says who won.</summary>
    [Signal]
    public delegate void MatchFinishedEventHandler(string result);

    /// <summary>Something went wrong running the match (e.g. a bot crashed).</summary>
    [Signal]
    public delegate void FailedEventHandler(string message);

    /// <summary>Open this match file instead (quickload, a save from the debug panel).</summary>
    [Signal]
    public delegate void LoadRequestedEventHandler(string path);

    /// <summary>Branch this match from right after command <paramref name="seq"/>.</summary>
    [Signal]
    public delegate void BranchRequestedEventHandler(int seq);

    /// <summary>Move this match to a shared file (a seat was handed to an LLM).</summary>
    [Signal]
    public delegate void ShareRequestedEventHandler();

    /// <summary>Open the settings screen.</summary>
    [Signal]
    public delegate void SettingsRequestedEventHandler();

    /// <summary>Leave the match for the main menu.</summary>
    [Signal]
    public delegate void MainMenuRequestedEventHandler();

    /// <summary>Quit the game.</summary>
    [Signal]
    public delegate void QuitRequestedEventHandler();

    /// <summary>Whether an animation, a submit in flight, the curtain, or the menu is in the way of input.</summary>
    private bool Busy => _player.IsPlaying || _submitting || _curtain.IsUp || _menu.IsOpen;

    /// <summary>
    /// The update on screen when it's waiting on input from a human seat (nothing playing or queued, the curtain
    /// down, the menu closed); otherwise <c>null</c>.
    /// </summary>
    public SeatUpdate? AwaitingInput =>
        !Busy && !_pumping && _pending.Count == 0 && _current is { Legal: not null, View.Outcome: null } current
            && ReferenceEquals(current, _session.Current) && _session.IsHuman(current.View.Seat)
            ? current
            : null;

    /// <summary>The update on screen, whether or not it waits on input.</summary>
    public SeatUpdate? Current => _current;

    /// <summary>The line the prompt shows: the last input problem, or what the shown seat is asked to do.</summary>
    public string PromptLine => _input?.Message
        ?? (_current is SeatUpdate update ? HudText.Prompt(update, LabelOf) : "");

    /// <summary>The hotseat curtain's ready button text while the curtain is up; otherwise <c>null</c>.</summary>
    public string? CurtainButton => _curtain.IsUp ? _curtain.ReadyText : null;

    /// <summary>Gives the screen its match. Call before adding it to the tree.</summary>
    /// <param name="open">The match.</param>
    /// <param name="saves">Where quicksaves go and saves are listed from.</param>
    /// <param name="settings">Player settings (speed, auto-skip, curtain).</param>
    /// <param name="saveSettings">Called when the player changes a setting on this screen (the speed button).</param>
    /// <param name="seatOptions">Controllers the debug panel offers for a seat (<c>human</c>, <c>llm</c>, <c>bot:…</c>).</param>
    public void Initialize(
        OpenMatch open,
        SaveLocations saves,
        ClientSettings settings,
        Action<ClientSettings> saveSettings,
        IReadOnlyList<string> seatOptions)
    {
        _open = open;
        _session = open.Session;
        _saves = saves;
        _seatOptions = seatOptions;
        _settings = settings;
        _saveSettings = saveSettings;
    }

    /// <inheritdoc />
    public override void _Ready()
    {
        _board.TileClicked += OnTileClicked;
        _board.TileHovered += _ => RedrawIfIdle();
        _hud.SubmitPressed += SubmitOrders;
        _hud.ActionPressed += OnAction;
        _hud.UnitPressed += OnRosterUnit;
        _hud.SpeedPressed += CycleSpeed;
        _hud.MenuPressed += OpenMenu;
        _draftPanel.AddPressed += (type, reserve) => ChangeDraft(draft => draft.Add(type, reserve));
        _draftPanel.RemovePressed += (type, reserve) => ChangeDraft(draft =>
        {
            draft.Remove(type, reserve);
            return null;
        });
        _draftPanel.MovePressed += (type, fromReserve) => ChangeDraft(draft => draft.Move(type, fromReserve));
        _draftPanel.ClearPressed += () => ChangeDraft(draft =>
        {
            draft.Clear();
            return null;
        });
        _draftPanel.SuggestPressed += Suggest;
        _draftPanel.SubmitPressed += SubmitOrders;
        _menu.SettingsPressed += () => EmitSignal(SignalName.SettingsRequested);
        _menu.MainMenuPressed += () => EmitSignal(SignalName.MainMenuRequested);
        _menu.QuitPressed += () => EmitSignal(SignalName.QuitRequested);
        _curtain.Dismissed += Pump;
        GetViewport().SizeChanged += FitCamera;
        _session.Updated += OnSessionUpdated;
        _session.ShownChanged += OnShownChanged;
        if (_open.SharedFile is { } file)
        {
            file.Diverged += OnDiverged;
        }

        _debug.GodViewToggled += _ => Redraw();
        _debug.SeatChosen += OnSeatChosen;
        _debug.QuicksavePressed += Quicksave;
        _debug.QuickloadPressed += Quickload;
        _debug.LoadChosen += path => EmitSignal(SignalName.LoadRequested, path);
        _debug.BranchChosen += seq => EmitSignal(SignalName.BranchRequested, seq);
        _debug.SetSeatOptions(_seatOptions);
        _debug.Visible = false;
        _draftPanel.Visible = false;

        _board.SetMap(_session.Current.View.Map);
        FitCamera();
        _hud.ShowSpeed(_settings.Speed);
        _hud.ShowNotice(StartNotice(), 8);
        Show(_session.Current);
        Run(async () =>
        {
            await _session.SetAutoSkipAsync(_settings.AutoSkip);
            await _session.StartAsync();
        });
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        if (_llmSince is not DateTime since || _current is not SeatUpdate update || _player.IsPlaying)
        {
            return;
        }

        // Refresh the waiting line once a second, not every frame.
        int seconds = (int)(DateTime.UtcNow - since).TotalSeconds;
        if (seconds != _llmSeconds)
        {
            _llmSeconds = seconds;
            _hud.ShowStatus(
                HudText.Status(update.View, _session.Rules),
                $"{HudText.Prompt(update, LabelOf)} The LLM is thinking… {seconds}s");
        }
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        _session.Updated -= OnSessionUpdated;
        _session.ShownChanged -= OnShownChanged;
        if (_open.SharedFile is { } file)
        {
            file.Diverged -= OnDiverged;
        }

        GetViewport().SizeChanged -= FitCamera;
    }

    /// <inheritdoc />
    public override void _UnhandledInput(InputEvent @event)
    {
        if (_curtain.IsUp)
        {
            return;
        }

        if (@event is InputEventKey && @event.IsActionPressed("cancel"))
        {
            Cancel();
        }
        else if (_menu.IsOpen)
        {
            return;
        }
        else if (@event.IsActionPressed("skip_animation"))
        {
            _player.Skip();
        }
        else if (@event.IsActionPressed("toggle_debug"))
        {
            ToggleDebug();
        }
        else if (@event.IsActionPressed("quicksave"))
        {
            Quicksave();
        }
        else if (@event.IsActionPressed("quickload"))
        {
            Quickload();
        }
        else if (@event.IsActionPressed("submit"))
        {
            SubmitOrders();
        }
        else if (@event.IsActionPressed("wait_action"))
        {
            OnAction("wait");
        }
        else if (@event.IsActionPressed("delay_action"))
        {
            OnAction("delay");
        }
        else
        {
            for (int slot = 0; slot < 9; slot++)
            {
                if (@event.IsActionPressed($"ability_{slot + 1}"))
                {
                    OnAction(slot.ToString(CultureInfo.InvariantCulture));
                }
            }
        }
    }

    /// <summary>Opens or closes the debug panel.</summary>
    public void ToggleDebug()
    {
        _debug.Visible = !_debug.Visible;
        RefreshDebug();
    }

    /// <summary>Uses changed settings (from the settings screen).</summary>
    public void ApplySettings(ClientSettings settings)
    {
        bool autoSkipChanged = settings.AutoSkip != _settings.AutoSkip;
        _settings = settings;
        _hud.ShowSpeed(settings.Speed);
        if (autoSkipChanged)
        {
            Run(() => _session.SetAutoSkipAsync(settings.AutoSkip));
        }
    }

    /// <summary>What's public about the opponents' drafts, for the draft screen's header.</summary>
    private static string OpponentLimits(PlayerView view) =>
        string.Join(" · ", view.Opponents()
            .Select(seat => view.Players[seat])
            .Select(them =>
            {
                string races = them.AllowedRaces is { } allowed ? string.Join(", ", allowed) : "any race";
                return $"{them.Seat}: budget {them.DraftBudget}, starting cap {them.StartingCap}, {races}";
            }));

    private void OnSessionUpdated(SeatUpdate update) => MainThread.Post(() =>
    {
        _pending.Enqueue(new Queued(update, null));
        Pump();
    });

    private void OnShownChanged(Seat seat) => MainThread.Post(() =>
    {
        _pending.Enqueue(new Queued(null, seat));
        Pump();
    });

    /// <summary>Plays queued updates one after another, stopping at a raised curtain until it's dismissed.</summary>
    private async void Pump()
    {
        if (_pumping || _curtain.IsUp)
        {
            return;
        }

        _pumping = true;
        try
        {
            while (_pending.TryDequeue(out Queued item))
            {
                if (item.Curtain is Seat next)
                {
                    if (_settings.Curtain)
                    {
                        // Dismissing the curtain calls Pump again for the rest of the queue.
                        _menu.Close();
                        _curtain.Raise(next);
                        return;
                    }
                }
                else if (item.Update is SeatUpdate update)
                {
                    await _player.Play(
                        TimelineBuilder.Build(update.Events),
                        _board,
                        _hud,
                        _settings.Speed,
                        update.View.Seat,
                        _session.Rules);
                    Show(update);
                }
            }
        }
        catch (Exception ex)
        {
            EmitSignal(SignalName.Failed, ex.ToString());
        }
        finally
        {
            _pumping = false;
        }
    }

    /// <summary>Snaps to <paramref name="update"/> and sets up input for the decision it asks for.</summary>
    private void Show(SeatUpdate update)
    {
        if (!ReferenceEquals(update.View.Map, _current?.View.Map))
        {
            _board.SetMap(update.View.Map);
        }

        _current = update;
        _llmSince = update.Legal is null && update.View.Outcome is null && WaitingOnLlm(update) ? DateTime.UtcNow : null;
        _llmSeconds = -1;
        _input = new DecisionInput(update);
        Redraw();
        RefreshDebug();

        if (update.View.Outcome is not null && _pending.Count == 0 && !_finished)
        {
            _finished = true;
            string result = HudText.Prompt(update, LabelOf);
            _menu.Open(result, over: true);
            EmitSignal(SignalName.MatchFinished, result);
        }
    }

    private void RedrawIfIdle()
    {
        if (!_player.IsPlaying && _pending.Count == 0)
        {
            Redraw();
        }
    }

    private void Redraw()
    {
        if (_current is not SeatUpdate update || _input is not DecisionInput input)
        {
            return;
        }

        BoardModel model = _debug.GodView
            ? GodView.Build(_session.Match.State, update.View.Seat, _board.Hovered)
            : BoardModel.Build(update.View, _session.Rules, input.Moves, input.Actions, _board.Hovered, input.Placement);
        _board.Render(model);
        _hud.ShowStatus(HudText.Status(update.View, _session.Rules), input.Message ?? HudText.Prompt(update, LabelOf));
        _hud.ShowPlayers(update.View.Phase == Phase.Draft ? [] : HudText.Players(update.View));
        _hud.ShowHint(model.Hint);

        _draftPanel.Visible = input.Draft is not null;
        if (input.Draft is not null)
        {
            _draftPanel.Show(input.Draft, _session.Rules, OpponentLimits(update.View), input.Message);
        }

        _hud.ShowActions(input.ActionButtons);
        _hud.ShowRoster(input.Roster);
        IReadOnlyList<string>? problems = input.SubmitProblems;
        _hud.ShowSubmit(problems is not null, problems is null ? "" : string.Join(" ", problems));
    }

    /// <summary>Fills the debug panel, if it is open.</summary>
    private void RefreshDebug()
    {
        if (!_debug.Visible)
        {
            return;
        }

        LocalMatch match = _session.Match;
        GameState state = match.State;
        IReadOnlyList<LoggedEvent> events = match.Events;
        _debug.Show(
            StateHash.Compute(state)[..12],
            state.Seats.ToDictionary(seat => seat, seat => match.ControllerOf(seat).Label),
            DebugText.Timeline(match.ToRecord(), events),
            [.. events.Select(DebugText.Line)],
            DebugText.Hidden(state, _session.Shown));
        _debug.ShowSaves(SaveSummary.In(_saves.Folder).Select(save => (save.Path, save.Text)));
    }

    /// <summary>Esc: close the menu, or drop the current pick if there is one, or open the menu.</summary>
    private void Cancel()
    {
        if (_menu.IsOpen)
        {
            _menu.Close();
        }
        else if (_input?.Cancel() == true)
        {
            RedrawIfIdle();
        }
        else
        {
            OpenMenu();
        }
    }

    private void OpenMenu()
    {
        if (_curtain.IsUp)
        {
            return;
        }

        bool over = _current?.View.Outcome is not null;
        _menu.Open(over && _current is SeatUpdate update ? HudText.Prompt(update, LabelOf) : "Paused", over);
    }

    private void Quicksave()
    {
        _session.SaveTo(_saves.Quicksave);
        _hud.ShowNotice($"Saved {_saves.Quicksave}", 3);
        RefreshDebug();
    }

    private void Quickload()
    {
        if (File.Exists(_saves.Quicksave))
        {
            EmitSignal(SignalName.LoadRequested, _saves.Quicksave);
        }
        else
        {
            _hud.ShowNotice("No quicksave yet (F5 makes one).", 3);
        }
    }

    /// <summary>Hands a seat to another controller; an LLM needs the match on a shared file first.</summary>
    private void OnSeatChosen(int seat, string label)
    {
        SeatController controller = SeatController.Parse(label);
        Run(async () =>
        {
            await _session.Match.SetControllerAsync((Seat)seat, controller);
            if (controller.Kind == SeatControllerKind.Llm && _open.SharedFile is null)
            {
                MainThread.Post(() => EmitSignal(SignalName.ShareRequested));
            }
            else
            {
                MainThread.Post(RefreshDebug);
            }
        });
    }

    private void OnTileClicked(Vector2I cell, bool secondary)
    {
        if (_input is not null && !Busy && !_debug.GodView)
        {
            SubmitOrRedraw(_input.Click(cell.ToPoint(), secondary));
        }
    }

    private void OnAction(string action)
    {
        if (Busy)
        {
            return;
        }

        if (action == DecisionInput.SuggestAction)
        {
            Suggest();
        }
        else if (_input is not null)
        {
            SubmitOrRedraw(_input.Action(action));
        }
    }

    private void OnRosterUnit(int unitId)
    {
        _input?.ChooseRosterUnit(unitId);
        Redraw();
    }

    /// <summary>Changes the draft and shows any problem the change ran into.</summary>
    private void ChangeDraft(Func<DraftBuilder, string?> change)
    {
        if (_input is not null && !Busy)
        {
            _input.ChangeDraft(change);
            Redraw();
        }
    }

    /// <summary>Fills the draft or placement with what a bot would choose; the player can still change it.</summary>
    private void Suggest()
    {
        if (_input is not null && !Busy)
        {
            _input.Suggest(_session.Suggest(SuggestProfile, Random.Shared.Next()));
            Redraw();
        }
    }

    private void SubmitOrders()
    {
        if (!Busy && _input?.Submit() is ICommand command)
        {
            Submit(command);
        }
    }

    /// <summary>Submits what input chose, or redraws when it chose nothing yet.</summary>
    private void SubmitOrRedraw(ICommand? command)
    {
        if (command is not null)
        {
            Submit(command);
        }
        else
        {
            Redraw();
        }
    }

    private void Submit(ICommand command) => Run(async () =>
    {
        _submitting = true;
        try
        {
            if (await _session.SubmitAsync(command) is RuleViolation violation)
            {
                MainThread.Post(() =>
                {
                    _input?.Reject(violation);
                    Redraw();
                });
            }
        }
        finally
        {
            _submitting = false;
        }
    });

    private void CycleSpeed()
    {
        int next = (Array.IndexOf(_speeds, _settings.Speed) + 1) % _speeds.Length;
        _settings = _settings with { Speed = _speeds[next] };
        _hud.ShowSpeed(_settings.Speed);
        _saveSettings(_settings);
    }

    private void FitCamera()
    {
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        Vector2 board = _board.PixelSize;
        if (board.X <= 0 || board.Y <= 0)
        {
            return;
        }

        float zoom = Mathf.Min(viewport.X / (board.X + 32), (viewport.Y - HudTop - HudBottom) / board.Y);
        _camera.Zoom = new Vector2(zoom, zoom);
        _camera.Position = board / 2 - new Vector2(0, (HudTop - HudBottom) / 2 / zoom);
    }

    /// <summary>Where the match is saved, any resume warning, and how to hand an LLM seat to Claude.</summary>
    private string StartNotice()
    {
        List<string> lines = [];
        if (_session.Match.ResumeWarning is string warning)
        {
            lines.Add(warning);
        }

        if (_open.SharedFile is { } file)
        {
            string relative = $"{Path.GetFileName(Path.GetDirectoryName(file.Path))}/{Path.GetFileName(file.Path)}";
            Seat[] llms = [.. _session.Match.State.Seats
                .Where(seat => _session.Match.ControllerOf(seat).Kind == SeatControllerKind.Llm)];
            lines.Add(llms.Length > 0
                ? $"Ask Claude to play {string.Join(" and ", llms)} in {relative} (play-fantactics skill)."
                : $"Playing on {relative}.");
        }
        else
        {
            lines.Add($"Autosaving to {Path.GetFileName(_saves.Folder)}/{Path.GetFileName(_saves.Autosave)}.");
        }

        return string.Join('\n', lines);
    }

    private bool WaitingOnLlm(SeatUpdate update) =>
        update.View.PendingDecisions.Any(d => _session.Match.ControllerOf(d.Seat).Kind == SeatControllerKind.Llm);

    private void OnDiverged(string reason) =>
        MainThread.Post(() => _hud.ShowNotice($"{reason} Syncing stopped; the match continues here only.", 20));

    private string LabelOf(Seat seat) => _session.Match.ControllerOf(seat).Label;

    /// <summary>Runs match work off the main thread, reporting failures through <see cref="Failed"/>.</summary>
    private void Run(Func<Task> work) => Task.Run(async () =>
    {
        try
        {
            await work();
        }
        catch (Exception ex)
        {
            MainThread.Post(() => EmitSignal(SignalName.Failed, ex.ToString()));
        }
    });

    /// <summary>An item in the playback queue: an update to play, or a curtain to raise for the next seat.</summary>
    /// <param name="Update">The update, or <c>null</c> for a curtain.</param>
    /// <param name="Curtain">The seat to raise the curtain for, or <c>null</c> for an update.</param>
    private readonly record struct Queued(SeatUpdate? Update, Seat? Curtain);
}
