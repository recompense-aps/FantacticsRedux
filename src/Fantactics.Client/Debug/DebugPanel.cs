using Fantactics.Client.Logic.Debug;
using Fantactics.Core;
using Godot;

namespace Fantactics.Client.Debug;

/// <summary>
/// The testing panel (F1): state hash, god view, who controls each seat, quicksave/quickload, the saves folder, the
/// command timeline (click to branch from there), the event log, and hidden information. It shows what it's given
/// and reports what was clicked.
/// </summary>
public partial class DebugPanel : PanelContainer
{
    private const int MaxEvents = 300;

    private readonly List<string> _files = [];
    private readonly List<int> _timelineSeqs = [];
    private IReadOnlyList<string> _seatOptions = [];

    [Export]
    private Label _hash = null!;

    [Export]
    private CheckBox _godView = null!;

    [Export]
    private OptionButton _p1 = null!;

    [Export]
    private OptionButton _p2 = null!;

    [Export]
    private Label _p3Label = null!;

    [Export]
    private OptionButton _p3 = null!;

    [Export]
    private Label _p4Label = null!;

    [Export]
    private OptionButton _p4 = null!;

    [Export]
    private Button _quicksave = null!;

    [Export]
    private Button _quickload = null!;

    [Export]
    private ItemList _saves = null!;

    [Export]
    private ItemList _timeline = null!;

    [Export]
    private ItemList _events = null!;

    [Export]
    private Label _hidden = null!;

    /// <summary>God view was turned on or off.</summary>
    [Signal]
    public delegate void GodViewToggledEventHandler(bool on);

    /// <summary>A seat was handed to another controller (its label, e.g. <c>bot:captain</c>).</summary>
    [Signal]
    public delegate void SeatChosenEventHandler(int seat, string label);

    /// <summary>Quicksave was pressed.</summary>
    [Signal]
    public delegate void QuicksavePressedEventHandler();

    /// <summary>Quickload was pressed.</summary>
    [Signal]
    public delegate void QuickloadPressedEventHandler();

    /// <summary>A save file was double-clicked.</summary>
    [Signal]
    public delegate void LoadChosenEventHandler(string path);

    /// <summary>A timeline entry was clicked: branch from right after command <paramref name="seq"/>.</summary>
    [Signal]
    public delegate void BranchChosenEventHandler(int seq);

    /// <summary>Whether god view is on.</summary>
    public bool GodView => _godView.ButtonPressed;

    /// <inheritdoc />
    public override void _Ready()
    {
        _godView.Toggled += on => EmitSignal(SignalName.GodViewToggled, on);
        foreach ((Seat seat, OptionButton picker) in Pickers())
        {
            picker.ItemSelected += index => EmitSignal(SignalName.SeatChosen, (int)seat, _seatOptions[(int)index]);
        }

        _quicksave.Pressed += () => EmitSignal(SignalName.QuicksavePressed);
        _quickload.Pressed += () => EmitSignal(SignalName.QuickloadPressed);
        _saves.ItemActivated += index => EmitSignal(SignalName.LoadChosen, _files[(int)index]);
        _timeline.ItemActivated += index => EmitSignal(SignalName.BranchChosen, _timelineSeqs[(int)index]);
    }

    /// <summary>Sets the controllers a seat can be handed to (<c>human</c>, <c>llm</c>, <c>bot:…</c>).</summary>
    public void SetSeatOptions(IReadOnlyList<string> options)
    {
        _seatOptions = options;
        foreach ((_, OptionButton seat) in Pickers())
        {
            seat.Clear();
            foreach (string option in options)
            {
                seat.AddItem(option);
            }
        }
    }

    /// <summary>Shows the match's current debug information.</summary>
    /// <param name="hash">A short state hash.</param>
    /// <param name="labels">Who controls each seat.</param>
    /// <param name="timeline">The commands so far.</param>
    /// <param name="events">Event log lines.</param>
    /// <param name="hidden">Hidden information lines.</param>
    public void Show(
        string hash,
        IReadOnlyDictionary<Seat, string> labels,
        IReadOnlyList<TimelineEntry> timeline,
        IReadOnlyList<string> events,
        IEnumerable<string> hidden)
    {
        _hash.Text = $"State {hash}";
        string[] missing = [.. labels.Values.Distinct().Where(label => !_seatOptions.Contains(label))];
        if (missing.Length > 0)
        {
            SetSeatOptions([.. _seatOptions, .. missing]);
        }

        foreach ((Seat seat, OptionButton picker) in Pickers())
        {
            picker.Visible = labels.ContainsKey(seat);
            if (picker.Visible)
            {
                picker.Select(_seatOptions.ToList().IndexOf(labels[seat]));
            }
        }

        _p3Label.Visible = _p3.Visible;
        _p4Label.Visible = _p4.Visible;

        _timeline.Clear();
        _timelineSeqs.Clear();
        foreach (TimelineEntry entry in timeline)
        {
            _timeline.AddItem($"#{entry.Seq} T{entry.Turn} {entry.Seat}: {entry.Text}");
            _timelineSeqs.Add(entry.Seq);
        }

        if (_timeline.ItemCount > 0)
        {
            _timeline.Select(_timeline.ItemCount - 1);
            _timeline.EnsureCurrentIsVisible();
        }

        _events.Clear();
        foreach (string line in events.Skip(Math.Max(0, events.Count - MaxEvents)))
        {
            _events.AddItem(line);
        }

        if (_events.ItemCount > 0)
        {
            _events.Select(_events.ItemCount - 1);
            _events.EnsureCurrentIsVisible();
        }

        _hidden.Text = string.Join("\n", hidden);
    }

    /// <summary>Lists the save files, as (path, summary) pairs.</summary>
    public void ShowSaves(IEnumerable<(string Path, string Summary)> saves)
    {
        _saves.Clear();
        _files.Clear();
        foreach ((string path, string summary) in saves)
        {
            _saves.AddItem(summary);
            _files.Add(path);
        }
    }

    private IEnumerable<(Seat Seat, OptionButton Picker)> Pickers() =>
        [(Seat.P1, _p1), (Seat.P2, _p2), (Seat.P3, _p3), (Seat.P4, _p4)];
}
