using Godot;

namespace Fantactics.Client.Menus;

/// <summary>The title screen: quick match, new match, continue the autosave, load, settings, quit.</summary>
public partial class MainMenu : Control
{
    [Export]
    private Button _quickMatch = null!;

    [Export]
    private Button _newMatch = null!;

    [Export]
    private Button _continue = null!;

    [Export]
    private Button _load = null!;

    [Export]
    private Button _settings = null!;

    [Export]
    private Button _quit = null!;

    [Export]
    private Label _footer = null!;

    [Export]
    private Label _error = null!;

    /// <summary>Start a match against <c>bot:captain@easy</c> with a bot drafting for you.</summary>
    [Signal]
    public delegate void QuickMatchPressedEventHandler();

    /// <summary>Open the new-match screen.</summary>
    [Signal]
    public delegate void NewMatchPressedEventHandler();

    /// <summary>Load the autosave.</summary>
    [Signal]
    public delegate void ContinuePressedEventHandler();

    /// <summary>Open the load screen.</summary>
    [Signal]
    public delegate void LoadPressedEventHandler();

    /// <summary>Open the settings.</summary>
    [Signal]
    public delegate void SettingsPressedEventHandler();

    /// <summary>Quit the game.</summary>
    [Signal]
    public delegate void QuitPressedEventHandler();

    /// <inheritdoc />
    public override void _Ready()
    {
        _quickMatch.Pressed += () => EmitSignal(SignalName.QuickMatchPressed);
        _newMatch.Pressed += () => EmitSignal(SignalName.NewMatchPressed);
        _continue.Pressed += () => EmitSignal(SignalName.ContinuePressed);
        _load.Pressed += () => EmitSignal(SignalName.LoadPressed);
        _settings.Pressed += () => EmitSignal(SignalName.SettingsPressed);
        _quit.Pressed += () => EmitSignal(SignalName.QuitPressed);
        _newMatch.GrabFocus();
    }

    /// <summary>Shows what Continue would load and where saves go.</summary>
    /// <param name="autosave">The autosave's summary, or <c>null</c> when there is none (Continue is disabled).</param>
    /// <param name="savesFolder">The saves folder.</param>
    public void ShowSaves(string? autosave, string savesFolder)
    {
        _continue.Disabled = autosave is null;
        _continue.TooltipText = autosave ?? "No autosave yet.";
        _footer.Text = $"Saves: {savesFolder}";
    }

    /// <summary>Shows why something couldn't open.</summary>
    public void ShowError(string message) => _error.Text = message;
}
