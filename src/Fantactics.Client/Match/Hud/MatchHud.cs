using Fantactics.Client.Common;
using Fantactics.Core;
using Godot;

namespace Fantactics.Client.Match.Hud;

/// <summary>
/// The match HUD: status and prompt lines, the player list (with more than two players), the hovered tile's hint,
/// the action bar, roster buttons (deploys, placement), the Submit button, the speed toggle, the menu button, and a
/// banner. It only shows what it's given and reports button presses.
/// </summary>
public partial class MatchHud : Control
{
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

    /// <inheritdoc />
    public override void _Ready()
    {
        _submit.Pressed += () => EmitSignal(SignalName.SubmitPressed);
        _speed.Pressed += () => EmitSignal(SignalName.SpeedPressed);
        _menu.Pressed += () => EmitSignal(SignalName.MenuPressed);
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
            };
            line.AddThemeFontSizeOverride("font_size", 14);
            line.AddThemeColorOverride("font_color", SeatColors.Of(seat).Lightened(0.35f));
            _players.AddChild(line);
        }
    }

    /// <summary>Sets the hint for the hovered tile.</summary>
    public void ShowHint(string? hint) => _hint.Text = hint ?? "";

    /// <summary>Shows action buttons as (action, label) pairs; empty hides the bar.</summary>
    public void ShowActions(IReadOnlyList<(string Action, string Label)> actions)
    {
        Refill(_actions, actions.Select(a => (a.Label, (Action)(() => EmitSignal(SignalName.ActionPressed, a.Action)), false)));
    }

    /// <summary>Shows roster buttons (reserve units to deploy, or starting units to place): (unit, label, chosen).</summary>
    public void ShowRoster(IReadOnlyList<(int UnitId, string Label, bool Chosen)> units)
    {
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
