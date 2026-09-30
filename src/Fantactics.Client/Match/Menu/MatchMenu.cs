using Godot;

namespace Fantactics.Client.Match.Menu;

/// <summary>
/// The in-match menu (Esc or the Menu button), also shown with the result when the match ends. The match keeps
/// running behind it; bots and the LLM don't pause.
/// </summary>
public partial class MatchMenu : Control
{
    [Export]
    private Label _title = null!;

    [Export]
    private Button _resume = null!;

    [Export]
    private Button _settings = null!;

    [Export]
    private Button _mainMenu = null!;

    [Export]
    private Button _quit = null!;

    /// <summary>Open the settings.</summary>
    [Signal]
    public delegate void SettingsPressedEventHandler();

    /// <summary>Leave the match for the main menu.</summary>
    [Signal]
    public delegate void MainMenuPressedEventHandler();

    /// <summary>Quit the game.</summary>
    [Signal]
    public delegate void QuitPressedEventHandler();

    /// <summary>Whether the menu is open.</summary>
    public bool IsOpen => Visible;

    /// <inheritdoc />
    public override void _Ready()
    {
        Visible = false;
        _resume.Pressed += Close;
        _settings.Pressed += () => EmitSignal(SignalName.SettingsPressed);
        _mainMenu.Pressed += () => EmitSignal(SignalName.MainMenuPressed);
        _quit.Pressed += () => EmitSignal(SignalName.QuitPressed);
    }

    /// <summary>Opens the menu.</summary>
    /// <param name="title">The heading: <c>Paused</c>, or the match result.</param>
    /// <param name="over">Whether the match is over (Resume becomes "Look at the board").</param>
    public void Open(string title, bool over)
    {
        _title.Text = title;
        _resume.Text = over ? "Look at the board" : "Resume";
        Visible = true;
        _resume.GrabFocus();
    }

    /// <summary>Closes the menu.</summary>
    public void Close() => Visible = false;
}
