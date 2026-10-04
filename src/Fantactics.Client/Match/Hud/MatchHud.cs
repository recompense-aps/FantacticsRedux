using Fantactics.Client.Common;
using Fantactics.Client.Logic.Board;
using Fantactics.Client.Logic.Log;
using Fantactics.Core;
using Godot;

namespace Fantactics.Client.Match.Hud;

/// <summary>
/// The match HUD: status and prompt lines, the player list (with more than two players), the hovered tile's hint,
/// the action bar, roster buttons (deploys, placement), the Submit button, the speed toggle, the menu button, a
/// banner, and on the left the unit info panel above the player's log. It only shows what it's given and reports button presses.
/// </summary>
public partial class MatchHud : Control
{
    private LogLine? _lastLogLine;
    private int _logCount;

    [Export]
    private Label _status = null!;

    [Export]
    private Label _prompt = null!;

    [Export]
    private VBoxContainer _players = null!;

    [Export]
    private Label _hint = null!;

    [Export]
    private HBoxContainer _actions = null!;

    [Export]
    private Label _rosterTitle = null!;

    [Export]
    private HBoxContainer _reserve = null!;

    [Export]
    private Label _problems = null!;

    [Export]
    private Button _submit = null!;

    [Export]
    private Button _speed = null!;

    [Export]
    private Button _menu = null!;

    [Export]
    private Label _banner = null!;

    [Export]
    private Label _notice = null!;

    [Export]
    private Control _side = null!;

    [Export]
    private UnitInfoPanel _unitInfo = null!;

    [Export]
    private Control _log = null!;

    [Export]
    private ScrollContainer _logScroll = null!;

    [Export]
    private VBoxContainer _logLines = null!;

    [Export]
    private Button _logButton = null!;

    /// <summary>Submit was pressed.</summary>
    [Signal]
    public delegate void SubmitPressedEventHandler();

    /// <summary>An action bar button was pressed: <c>wait</c>, <c>delay</c>, or the ability's slot as a number.</summary>
    [Signal]
    public delegate void ActionPressedEventHandler(string action);

    /// <summary>A roster button (a reserve unit to deploy, or a starting unit to place) was pressed.</summary>
    [Signal]
    public delegate void UnitPressedEventHandler(int unitId);

    /// <summary>The speed button was pressed.</summary>
    [Signal]
    public delegate void SpeedPressedEventHandler();

    /// <summary>The menu button was pressed.</summary>
    [Signal]
    public delegate void MenuPressedEventHandler();

    /// <summary>The log button was pressed.</summary>
    [Signal]
    public delegate void LogPressedEventHandler();

    /// <summary>Screen pixels the log panel takes on the left, including its gap from the edge; 0 when hidden.</summary>
    public float LogWidth => _log.Visible ? _side.OffsetRight : 0;

    /// <inheritdoc />
    public override void _Ready()
    {
        _submit.Pressed += () => EmitSignal(SignalName.SubmitPressed);
        _speed.Pressed += () => EmitSignal(SignalName.SpeedPressed);
        _menu.Pressed += () => EmitSignal(SignalName.MenuPressed);
        _logButton.Pressed += () => EmitSignal(SignalName.LogPressed);
        _logScroll.GetVScrollBar().Changed += ScrollLogToEnd;
        _banner.Visible = false;
    }

    /// <summary>Sets the status and prompt lines.</summary>
    public void ShowStatus(string status, string prompt)
    {
        _status.Text = status;
        _prompt.Text = prompt;
    }

    /// <summary>
    /// Lists every player in their seat's color, one line each. Hidden with two players, whom the status line covers.
    /// </summary>
    public void ShowPlayers(IReadOnlyList<(Seat Seat, string Text)> players)
    {
        foreach (Node child in _players.GetChildren())
        {
            child.QueueFree();
        }

        _players.Visible = players.Count > 2;
        if (!_players.Visible)
        {
            return;
        }

        foreach ((Seat seat, string text) in players)
        {
            Label line = new()
            {
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Right,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                ThemeTypeVariation = "Caption",
            };
            line.AddThemeColorOverride("font_color", SeatColors.Of(seat).Lightened(0.35f));
            _players.AddChild(line);
        }
    }

    /// <summary>
    /// Shows the player's log, scrolled to the newest line. When <paramref name="lines"/> continues what's shown,
    /// only the new lines are added.
    /// </summary>
    public void ShowLog(IReadOnlyList<LogLine> lines)
    {
        bool continues = _logCount > 0 && lines.Count >= _logCount && ReferenceEquals(lines[_logCount - 1], _lastLogLine);
        if (!continues)
        {
            foreach (Node child in _logLines.GetChildren())
            {
                _logLines.RemoveChild(child);
                child.QueueFree();
            }

            _logCount = 0;
        }

        foreach (LogLine line in lines.Skip(_logCount))
        {
            _logLines.AddChild(new Label
            {
                Text = line.Text,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                ThemeTypeVariation = line.Heading ? "LogHeading" : "Caption",
            });
        }

        _logCount = lines.Count;
        _lastLogLine = lines.Count > 0 ? lines[^1] : null;
    }

    /// <summary>Shows or hides the log panel.</summary>
    public void ShowLogPanel(bool visible)
    {
        _log.Visible = visible;
        _logButton.SetPressedNoSignal(visible);
    }

    /// <summary>Describes a unit above the log, or hides the description for <c>null</c>.</summary>
    public void ShowUnitInfo(UnitInfo? info) => _unitInfo.Describe(info);

    /// <summary>Sets the hint for the hovered tile.</summary>
    public void ShowHint(string? hint) => _hint.Text = hint ?? "";

    /// <summary>Shows action buttons as (action, label) pairs; empty hides the bar.</summary>
    public void ShowActions(IReadOnlyList<(string Action, string Label)> actions)
    {
        Refill(_actions, actions.Select(a => (a.Label, (Action)(() => EmitSignal(SignalName.ActionPressed, a.Action)), false)));
    }

    /// <summary>
    /// Shows roster buttons (reserve units to deploy, or starting units to place) as (unit, label, chosen), after
    /// <paramref name="title"/>; an empty title is hidden.
    /// </summary>
    public void ShowRoster(string title, IReadOnlyList<(int UnitId, string Label, bool Chosen)> units)
    {
        _rosterTitle.Text = title;
        _rosterTitle.Visible = title.Length > 0;
        Refill(_reserve, units.Select(u => (u.Label, (Action)(() => EmitSignal(SignalName.UnitPressed, u.UnitId)), u.Chosen)));
    }

    /// <summary>Shows or hides Submit, with any problems that stop it.</summary>
    public void ShowSubmit(bool visible, string problems)
    {
        _submit.Visible = visible;
        _submit.Disabled = problems.Length > 0;
        _problems.Text = problems;
    }

    /// <summary>Shows the speed setting.</summary>
    public void ShowSpeed(double speed) => _speed.Text = speed <= 0 ? "Speed: instant" : $"Speed: {speed}×";

    /// <summary>Shows a banner across the screen.</summary>
    /// <returns>The tween hiding it, or <c>null</c> when there's no time to show it.</returns>
    public Tween? ShowBanner(string text, float seconds)
    {
        if (seconds <= 0)
        {
            return null;
        }

        _banner.Text = text;
        _banner.Visible = true;
        _banner.Modulate = Colors.White;
        Tween tween = CreateTween();
        tween.TweenInterval(seconds * 0.6f);
        tween.TweenProperty(_banner, "modulate:a", 0f, seconds * 0.4f);
        tween.TweenCallback(Callable.From(() => _banner.Visible = false));
        return tween;
    }

    /// <summary>Shows a notice in the top right (e.g. a resume warning or where a file was saved) that fades out.</summary>
    public void ShowNotice(string text, float seconds = 6)
    {
        _notice.Text = text;
        _notice.Modulate = _notice.Modulate with { A = 1 };
        Tween tween = CreateTween();
        tween.TweenInterval(seconds);
        tween.TweenProperty(_notice, "modulate:a", 0f, 1);
    }

    private void ScrollLogToEnd() => _logScroll.ScrollVertical = (int)_logScroll.GetVScrollBar().MaxValue;

    private static void Refill(HBoxContainer box, IEnumerable<(string Label, Action Pressed, bool Chosen)> buttons)
    {
        foreach (Node child in box.GetChildren())
        {
            box.RemoveChild(child);
            child.QueueFree();
        }

        foreach ((string label, Action pressed, bool chosen) in buttons)
        {
            Button button = new() { Text = label, ToggleMode = chosen, ButtonPressed = chosen, FocusMode = FocusModeEnum.None };
            button.Pressed += pressed;
            box.AddChild(button);
        }

        box.Visible = box.GetChildCount() > 0;
    }
}
