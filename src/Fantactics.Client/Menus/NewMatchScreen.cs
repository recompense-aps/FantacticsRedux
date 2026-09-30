using System.Collections.Immutable;
using System.Globalization;
using Fantactics.Client.Logic.Menus;
using Fantactics.Core;
using Godot;

namespace Fantactics.Client.Menus;

/// <summary>
/// Sets up a new match: map, each seat's player and draft limits, seed, whether a bot drafts for you, and an optional
/// match file. The owner reads <see cref="Form"/> when Start is pressed; Start is disabled while the form has problems.
/// </summary>
public partial class NewMatchScreen : Control
{
    private const string DraftByHand = "Draft by hand";

    private NewMatchForm _initial = null!;
    private IReadOnlyList<string> _maps = [];
    private IReadOnlyList<string> _races = [];
    private IReadOnlyList<string> _controllers = [];
    private IReadOnlyList<string> _difficulties = [];
    private IReadOnlyList<string> _draftProfiles = [];
    private Func<NewMatchForm, IReadOnlyList<string>> _problems = _ => [];

    [Export]
    private SeatColumn _p1 = null!;

    [Export]
    private SeatColumn _p2 = null!;

    [Export]
    private OptionButton _map = null!;

    [Export]
    private LineEdit _seed = null!;

    [Export]
    private OptionButton _draftAs = null!;

    [Export]
    private LineEdit _out = null!;

    [Export]
    private Label _message = null!;

    [Export]
    private Button _back = null!;

    [Export]
    private Button _start = null!;

    /// <summary>Start the match in <see cref="Form"/>.</summary>
    [Signal]
    public delegate void StartPressedEventHandler();

    /// <summary>Go back to the main menu.</summary>
    [Signal]
    public delegate void BackPressedEventHandler();

    /// <summary>The match as set up on the screen.</summary>
    public NewMatchForm Form
    {
        get
        {
            string draftAs = _draftProfiles[Math.Max(_draftAs.Selected, 0)];
            return new NewMatchForm(
                _maps[Math.Max(_map.Selected, 0)],
                ImmutableSortedDictionary.CreateRange([
                    KeyValuePair.Create(Seat.P1, _p1.Form),
                    KeyValuePair.Create(Seat.P2, _p2.Form)]),
                ulong.TryParse(_seed.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out ulong seed)
                    ? seed
                    : null,
                draftAs == DraftByHand ? null : draftAs,
                _out.Text.Trim() is { Length: > 0 } path ? path : null);
        }
    }

    /// <summary>Fills the screen. Call before adding it to the tree.</summary>
    /// <param name="form">The starting settings.</param>
    /// <param name="maps">Map names.</param>
    /// <param name="races">Every race in the rules.</param>
    /// <param name="controllers">Seat controller choices: <c>human</c>, <c>llm</c>, <c>bot:&lt;profile&gt;</c>.</param>
    /// <param name="difficulties">Bot difficulty presets.</param>
    /// <param name="draftProfiles">Bot profiles that can draft for human seats.</param>
    /// <param name="problems">Checks a form (see <see cref="NewMatchForm.Problems"/>).</param>
    public void Initialize(
        NewMatchForm form,
        IReadOnlyList<string> maps,
        IReadOnlyList<string> races,
        IReadOnlyList<string> controllers,
        IReadOnlyList<string> difficulties,
        IReadOnlyList<string> draftProfiles,
        Func<NewMatchForm, IReadOnlyList<string>> problems)
    {
        _initial = form;
        _maps = maps;
        _races = races;
        _controllers = controllers;
        _difficulties = difficulties;
        _draftProfiles = [DraftByHand, .. draftProfiles];
        _problems = problems;
    }

    /// <inheritdoc />
    public override void _Ready()
    {
        _p1.Setup(Seat.P1, _initial.Seats[Seat.P1], _controllers, _difficulties, _races);
        _p2.Setup(Seat.P2, _initial.Seats[Seat.P2], _controllers, _difficulties, _races);
        foreach (string map in _maps)
        {
            _map.AddItem(map);
        }

        foreach (string profile in _draftProfiles)
        {
            _draftAs.AddItem(profile == DraftByHand ? profile : $"Bot drafts: {profile}");
        }

        _map.Select(Math.Max(0, _maps.ToList().IndexOf(_initial.Map)));
        _draftAs.Select(_initial.DraftAs is string drafter ? Math.Max(0, _draftProfiles.ToList().IndexOf(drafter)) : 0);
        _seed.Text = _initial.Seed?.ToString(CultureInfo.InvariantCulture) ?? "";
        _out.Text = _initial.Out ?? "";

        // Connected only now, so filling the columns above doesn't check a half-built form.
        _p1.Changed += Check;
        _p2.Changed += Check;
        _map.ItemSelected += _ => Check();
        _draftAs.ItemSelected += _ => Check();
        _seed.TextChanged += _ => Check();
        _back.Pressed += () => EmitSignal(SignalName.BackPressed);
        _start.Pressed += () => EmitSignal(SignalName.StartPressed);
        Check();
        _start.GrabFocus();
    }

    /// <summary>Shows why the match couldn't start.</summary>
    public void ShowError(string message) => _message.Text = message;

    private void Check()
    {
        string seed = _seed.Text.Trim();
        List<string> problems = [.. _problems(Form)];
        if (seed.Length > 0 && !ulong.TryParse(seed, NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            problems.Add("The seed must be a whole number (or blank for a random one).");
        }

        _message.Text = string.Join("\n", problems);
        _start.Disabled = problems.Count > 0;
    }
}
