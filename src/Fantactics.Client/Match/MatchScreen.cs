using Fantactics.Client.Common;
using Fantactics.Client.Logic.Board;
using Fantactics.Client.Logic.Input;
using Fantactics.Client.Logic.Playback;
using Fantactics.Client.Logic.Session;
using Fantactics.Client.Logic.Settings;
using Fantactics.Client.Match.Board;
using Fantactics.Client.Match.Hud;
using Fantactics.Client.Match.Playback;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Geometry;
using Fantactics.Core.Hosting;
using Fantactics.Core.State;
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
    private ClientSession _session = null!;
    private ClientSettings _settings = new();
    private Action<ClientSettings> _saveSettings = _ => { };
    private SeatUpdate? _current;
    private MoveOrderBuilder? _moves;
    private ActionPicker? _actions;
    private bool _pumping;
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

    /// <summary>The match ended and its last update has played; <paramref name="result"/> says who won.</summary>
    [Signal]
    public delegate void MatchFinishedEventHandler(string result);

    /// <summary>Something went wrong running the match (e.g. a bot crashed).</summary>
    [Signal]
    public delegate void FailedEventHandler(string message);

    /// <summary>Gives the screen its match. Call before adding it to the tree.</summary>
    /// <param name="session">The match as this screen sees it.</param>
    /// <param name="settings">Player settings (speed, auto-skip).</param>
    /// <param name="saveSettings">Called when the player changes a setting.</param>
    public void Initialize(ClientSession session, ClientSettings settings, Action<ClientSettings> saveSettings)
    {
        _session = session;
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

        _board.SetMap(_session.Current.View.Map);
        FitCamera();
        _hud.ShowSpeed(_settings.Speed);
        Show(_session.Current);
        Run(async () =>
        {
            await _session.SetAutoSkipAsync(_settings.AutoSkip);
            await _session.StartAsync();
        });
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        _session.Updated -= OnSessionUpdated;
        GetViewport().SizeChanged -= FitCamera;
    }

    /// <inheritdoc />
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("skip_animation"))
        {
            _player.Skip();
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
        _message = null;
        _moves = update.Legal is { Decision: SubmitMoveOrdersDecision, Moves: MoveOptions moves }
            ? new MoveOrderBuilder(update.View, moves)
            : null;
        _actions = update.Legal is { Decision: ChooseUnitActionDecision decision } legal
            ? new ActionPicker(update.View, decision.UnitId, legal.Actions)
            : null;
        Redraw();

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

        BoardModel model = BoardModel.Build(update.View, _session.Rules, _moves, _actions, _board.Hovered);
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

    private void OnTileClicked(Vector2I cell, bool secondary)
    {
        if (_current is not SeatUpdate update || _player.IsPlaying || _submitting)
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

    private string LabelOf(Fantactics.Core.Seat seat) => _session.Match.ControllerOf(seat).Label;

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
