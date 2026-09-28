using Godot;

namespace Fantactics.Client.Match.Hud;

/// <summary>
/// The match HUD: status and prompt lines, the hovered tile's hint, the action bar, reserve (deploy) buttons, the
/// Submit button, the speed toggle, and a banner. It only shows what it's given and reports button presses.
/// </summary>
public partial class MatchHud : Control
{
    [Export]
    private Label _status = null!;

    [Export]
    private Label _prompt = null!;

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
    private Label _banner = null!;

    /// <summary>Submit was pressed.</summary>
    [Signal]
    public delegate void SubmitPressedEventHandler();

    /// <summary>An action bar button was pressed: <c>wait</c>, <c>delay</c>, or the ability's slot as a number.</summary>
    [Signal]
    public delegate void ActionPressedEventHandler(string action);

    /// <summary>A reserve unit's button was pressed.</summary>
    [Signal]
    public delegate void DeployPressedEventHandler(int unitId);

    /// <summary>The speed button was pressed.</summary>
    [Signal]
    public delegate void SpeedPressedEventHandler();

    /// <inheritdoc />
    public override void _Ready()
    {
        _submit.Pressed += () => EmitSignal(SignalName.SubmitPressed);
        _speed.Pressed += () => EmitSignal(SignalName.SpeedPressed);
        _banner.Visible = false;
    }

    /// <summary>Sets the status and prompt lines.</summary>
    public void ShowStatus(string status, string prompt)
    {
        _status.Text = status;
        _prompt.Text = prompt;
    }

    /// <summary>Sets the hint for the hovered tile.</summary>
    public void ShowHint(string? hint) => _hint.Text = hint ?? "";

    /// <summary>Shows action buttons as (action, label) pairs; empty hides the bar.</summary>
    public void ShowActions(IReadOnlyList<(string Action, string Label)> actions)
    {
        Refill(_actions, actions.Select(a => (a.Label, (Action)(() => EmitSignal(SignalName.ActionPressed, a.Action)), false)));
    }

    /// <summary>Shows reserve units that can deploy, as (unit, label, chosen) triples.</summary>
    public void ShowReserve(IReadOnlyList<(int UnitId, string Label, bool Chosen)> units)
    {
        Refill(_reserve, units.Select(u => (u.Label, (Action)(() => EmitSignal(SignalName.DeployPressed, u.UnitId)), u.Chosen)));
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
