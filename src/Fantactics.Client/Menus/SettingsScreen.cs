using Fantactics.Client.Logic.Settings;
using Godot;

namespace Fantactics.Client.Menus;

/// <summary>
/// Player settings: animation speed, auto-skip, and the hotseat curtain. Changes apply at once; the owner reads
/// <see cref="Settings"/> on <see cref="ChangedEventHandler"/> and saves them.
/// </summary>
public partial class SettingsScreen : Control
{
    private static readonly double[] _speeds = [1, 2, 4, 0];

    private ClientSettings _values = new();

    [Export]
    private OptionButton _speed = null!;

    [Export]
    private CheckBox _autoSkip = null!;

    [Export]
    private CheckBox _curtain = null!;

    [Export]
    private Label _saves = null!;

    [Export]
    private Button _close = null!;

    /// <summary>A setting changed.</summary>
    [Signal]
    public delegate void ChangedEventHandler();

    /// <summary>The screen was closed.</summary>
    [Signal]
    public delegate void ClosedEventHandler();

    /// <summary>The settings as shown.</summary>
    public ClientSettings Settings => _values;

    /// <inheritdoc />
    public override void _Ready()
    {
        Visible = false;
        foreach (double speed in _speeds)
        {
            _speed.AddItem(speed <= 0 ? "Instant" : $"{speed}×");
        }

        _speed.ItemSelected += index => Change(_values with { Speed = _speeds[index] });
        _autoSkip.Toggled += on => Change(_values with { AutoSkip = on });
        _curtain.Toggled += on => Change(_values with { Curtain = on });
        _close.Pressed += () =>
        {
            Visible = false;
            EmitSignal(SignalName.Closed);
        };
    }

    /// <inheritdoc />
    public override void _Input(InputEvent @event)
    {
        // _Input runs before the match screen's _UnhandledInput, so Esc closes this screen rather than the menu under it.
        if (Visible && @event is InputEventKey && @event.IsActionPressed("cancel"))
        {
            GetViewport().SetInputAsHandled();
            _close.EmitSignal(BaseButton.SignalName.Pressed);
        }
    }

    /// <summary>Shows the screen with <paramref name="settings"/>.</summary>
    /// <param name="settings">The current settings.</param>
    /// <param name="savesFolder">Where saves go (shown for reference; <c>--saves</c> changes it).</param>
    public void Open(ClientSettings settings, string savesFolder)
    {
        _values = settings;
        int speed = Array.IndexOf(_speeds, settings.Speed);
        _speed.Select(speed < 0 ? 0 : speed);
        _autoSkip.SetPressedNoSignal(settings.AutoSkip);
        _curtain.SetPressedNoSignal(settings.Curtain);
        _saves.Text = $"Saves: {savesFolder}";
        Visible = true;
        _close.GrabFocus();
    }

    private void Change(ClientSettings changed)
    {
        _values = changed;
        EmitSignal(SignalName.Changed);
    }
}
