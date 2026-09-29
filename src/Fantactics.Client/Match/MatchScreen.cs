using Fantactics.Client.Common;
using Fantactics.Client.Debug;
using Fantactics.Client.Logic.Board;
using Fantactics.Client.Logic.Debug;
using Fantactics.Client.Logic.Input;
using Fantactics.Client.Logic.Playback;
using Fantactics.Client.Logic.Session;
using Fantactics.Client.Logic.Settings;
using Fantactics.Client.Match.Board;
using Fantactics.Client.Match.Hud;
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
/// one, then the board and HUD snap to its view. Clicks and keys go to the move-order builder or action picker for
/// the decision the shown seat owes, and finished commands are submitted through the session.
/// </summary>
public partial class MatchScreen : Node
{
    /// <summary>Screen pixels the HUD takes at the top (status and prompt lines).</summary>
    private const float HudTop = 64;

    /// <summary>Screen pixels the HUD takes at the bottom (buttons).</summary>
    private const float HudBottom = 52;

    private static readonly double[] _speeds = [1, 2, 4, 0];

    private readonly Queue<SeatUpdate> _pending = new();
    private OpenMatch _open = null!;
    private ClientSession _session = null!;
    private SaveLocations _saves = null!;
    private IReadOnlyList<string> _seatOptions = [];
    private ClientSettings _settings = new();
    private Action<ClientSettings> _saveSettings = _ => { };
    private SeatUpdate? _current;
    private MoveOrderBuilder? _moves;
    private ActionPicker? _actions;
    private bool _pumping;
    private DateTime? _llmSince;
    private int _llmSeconds = -1;
    private bool _submitting;
    private string? _message;

    [Export]
    private BoardView _board = null!;

    [Export]
    private Camera2D _camera = null!;

    [Export]
    private EventPlayer _player = null!;

    [Export]
    private MatchHud _hud = null!;

    [Export]
    private DebugPanel _debug = null!;

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

    /// <summary>Gives the screen its match. Call before adding it to the tree.</summary>
    /// <param name="open">The match.</param>
    /// <param name="saves">Where quicksaves go and saves are listed from.</param>
    /// <param name="settings">Player settings (speed, auto-skip).</param>
    /// <param name="saveSettings">Called when the player changes a setting.</param>
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
        _hud.DeployPressed += OnDeploy;
        _hud.SpeedPressed += CycleSpeed;
        GetViewport().SizeChanged += FitCamera;
        _session.Updated += OnSessionUpdated;
        _debug.GodViewToggled += _ => Redraw();
        _debug.SeatChosen += OnSeatChosen;
        _debug.QuicksavePressed += Quicksave;
        _debug.QuickloadPressed += Quickload;
        _debug.LoadChosen += path => EmitSignal(SignalName.LoadRequested, path);
        _debug.BranchChosen += seq => EmitSignal(SignalName.BranchRequested, seq);
        _debug.SetSeatOptions(_seatOptions);
        _debug.Visible = false;


        _board.SetMap(_session.Current.View.Map);
        FitCamera();
        _hud.ShowSpeed(_settings.Speed);
        string where = _open.SharedFile is { } shared ? $"Playing on {shared.Path}" : $"Autosaving to {Path.GetFileName(_saves.Folder)}/{Path.GetFileName(_saves.Autosave)}";
        _hud.ShowNotice(_session.Match.ResumeWarning is string warning ? $"{warning}\n{where}" : where, 8);
        Show(_session.Current);
        Run(async () =>
        {
            await _session.SetAutoSkipAsync(_settings.AutoSkip);
            await _session.StartAsync();
        });
    }

    /// <summary>Opens or closes the debug panel.</summary>
    public void ToggleDebug()
    {
        _debug.Visible = !_debug.Visible;
        RefreshDebug();
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
            _hud.ShowStatus(HudText.Status(update.View, _session.Rules), $"{HudText.Prompt(update, LabelOf)} The LLM is thinking… {seconds}s");
        }
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        _session.Updated -= OnSessionUpdated;
        if (_open.SharedFile is { } file)
        {
            file.Diverged -= OnDiverged;
        }
        GetViewport().SizeChanged -= FitCamera;
    }

    /// <inheritdoc />
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("skip_animation"))
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
        else if (@event is InputEventKey && @event.IsActionPressed("cancel"))
        {
            _moves?.Deselect();
            _actions?.ClearAbility();
            RedrawIfIdle();
        }
        else
        {
            for (int slot = 0; slot < 9; slot++)
            {
                if (@event.IsActionPressed($"ability_{slot + 1}"))
                {
                    OnAction(slot.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            }
        }
    }

    private void OnSessionUpdated(SeatUpdate update) => MainThread.Post(() =>
    {
        _pending.Enqueue(update);
        Pump();
    });

    /// <summary>Plays queued updates one after another.</summary>
    private async void Pump()
    {
        if (_pumping)
        {
            return;
        }

        _pumping = true;
        try
        {
            while (_pending.TryDequeue(out SeatUpdate? update))
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
        _message = null;
        _moves = update.Legal is { Decision: SubmitMoveOrdersDecision, Moves: MoveOptions moves }
            ? new MoveOrderBuilder(update.View, moves)
            : null;
        _actions = update.Legal is { Decision: ChooseUnitActionDecision decision } legal
            ? new ActionPicker(update.View, decision.UnitId, legal.Actions)
            : null;
        Redraw();
        RefreshDebug();

        if (update.View.Outcome is not null && _pending.Count == 0)
        {
            EmitSignal(SignalName.MatchFinished, HudText.Prompt(update, LabelOf));
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
        if (_current is not SeatUpdate update)
        {
            return;
        }

        BoardModel model = _debug.GodView
            ? GodView.Build(_session.Match.State, update.View.Seat, _board.Hovered)
            : BoardModel.Build(update.View, _session.Rules, _moves, _actions, _board.Hovered);
        _board.Render(model);
        _hud.ShowStatus(HudText.Status(update.View, _session.Rules), _message ?? HudText.Prompt(update, LabelOf));
        _hud.ShowHint(model.Hint);

        List<(string, string)> actions = [];
        if (_actions is not null)
        {
            actions.AddRange(_actions.Abilities.Select((ability, slot) =>
                (slot.ToString(System.Globalization.CultureInfo.InvariantCulture), $"{slot + 1} {ability}")));
            if (_actions.Wait is not null)
            {
                actions.Add(("wait", "Wait (W)"));
            }

            if (_actions.Delay is not null)
            {
                actions.Add(("delay", "Delay (D)"));
            }
        }

        _hud.ShowActions(actions);
        _hud.ShowReserve(_moves is null
            ? []
            : [.. _moves.DeployOptions.Select(d => (d.UnitId, $"{d.Type} ({d.Cost})", _moves.Deploys.ContainsKey(d.UnitId)))]);
        _hud.ShowSubmit(_moves is not null, _moves is null ? "" : string.Join(" ", _moves.Problems));
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
            SeatExtensions.All.ToDictionary(seat => seat, seat => match.ControllerOf(seat).Label),
            DebugText.Timeline(match.ToRecord(), events),
            [.. events.Select(DebugText.Line)],
            DebugText.Hidden(state, _session.Shown));
        _debug.ShowSaves(Directory.Exists(_saves.Folder)
            ? Directory.GetFiles(_saves.Folder, "*.json")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Select(path => (path, DebugText.Summary(path)))
            : []);
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
        if (_current is not SeatUpdate update || _player.IsPlaying || _submitting || _debug.GodView)
        {
            return;
        }

        Point tile = cell.ToPoint();
        Unit? mine = update.View.Units.FirstOrDefault(u => u.IsOnField && u.Owner == update.View.Seat && u.Position == tile);
        _message = null;
        if (_moves is not null)
        {
            ClickForMoves(tile, mine, secondary);
        }
        else if (_actions is not null)
        {
            if (secondary)
            {
                _actions.ClearAbility();
            }
            else if (_actions.OptionAt(tile) is ActionOption option)
            {
                Submit(option.Command);
                return;
            }
        }

        Redraw();
    }

    private void ClickForMoves(Point tile, Unit? mine, bool secondary)
    {
        MoveOrderBuilder moves = _moves ?? throw new InvalidOperationException("No move orders are being built.");
        if (secondary)
        {
            int? arriving = moves.Deploys.FirstOrDefault(d => d.Value == tile).Key;
            if (mine is not null || arriving is > 0)
            {
                moves.Clear(mine?.Id ?? arriving ?? 0);
            }

            moves.Deselect();
        }
        else if (moves.Selected is not null && moves.Choose(tile) is string problem)
        {
            if (mine is null || !moves.Select(mine.Id))
            {
                _message = problem;
            }
        }
        else if (moves.Selected is null && mine is not null && !moves.Select(mine.Id))
        {
            _message = $"{mine.Type} can't move this turn.";
        }
    }

    private void OnAction(string action)
    {
        if (_actions is null || _player.IsPlaying || _submitting)
        {
            return;
        }

        ActionOption? option = action switch
        {
            "wait" => _actions.Wait,
            "delay" => _actions.Delay,
            _ => _actions.SelectAbility(int.Parse(action, System.Globalization.CultureInfo.InvariantCulture)),
        };
        if (option is not null)
        {
            Submit(option.Command);
        }
        else
        {
            Redraw();
        }
    }

    private void OnDeploy(int unitId)
    {
        _moves?.Select(unitId);
        Redraw();
    }

    private void SubmitOrders()
    {
        if (_moves is { Problems.Count: 0 } && !_player.IsPlaying && !_submitting)
        {
            Submit(_moves.Build());
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
                    _message = violation.Message;
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
            IEnumerable<Seat> llms = SeatExtensions.All.Where(seat => _session.Match.ControllerOf(seat).Kind == SeatControllerKind.Llm);
            lines.Add(llms.Any()
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

    private void OnDiverged(string reason) => MainThread.Post(() => _hud.ShowNotice($"{reason} Syncing stopped; the match continues here only.", 20));

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
}
