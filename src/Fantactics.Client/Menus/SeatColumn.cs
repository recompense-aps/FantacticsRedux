using System.Collections.Immutable;
using Fantactics.Client.Logic.Menus;
using Fantactics.Core;
using Godot;

namespace Fantactics.Client.Menus;

/// <summary>One seat's column on the new-match screen: who plays it, bot difficulty, allowed races, budget, and cap.</summary>
public partial class SeatColumn : VBoxContainer
{
    private const string DefaultDifficulty = "default";

    private readonly List<CheckBox> _raceBoxes = [];
    private IReadOnlyList<string> _controllers = [];
    private IReadOnlyList<string> _difficulties = [];

    [Export]
    private Label _title = null!;

    [Export]
    private OptionButton _controller = null!;

    [Export]
    private OptionButton _difficulty = null!;

    [Export]
    private HFlowContainer _races = null!;

    [Export]
    private SpinBox _budget = null!;

    [Export]
    private SpinBox _startingCap = null!;

    /// <summary>Something in the column changed.</summary>
    [Signal]
    public delegate void ChangedEventHandler();

    /// <summary>The seat's settings as shown.</summary>
    public SeatForm Form
    {
        get
        {
            string controller = _controllers[Math.Max(_controller.Selected, 0)];
            string difficulty = _difficulties[Math.Max(_difficulty.Selected, 0)];
            ImmutableSortedSet<string> races = [.. _raceBoxes.Where(box => box.ButtonPressed).Select(box => box.Text)];
            return new SeatForm(
                HasDifficulty(controller) && difficulty != DefaultDifficulty ? $"{controller}@{difficulty}" : controller,
                races.Count == _raceBoxes.Count ? null : races,
                (int)_budget.Value,
                (int)_startingCap.Value);
        }
    }

    /// <summary>Fills the column. Call once, after the column is in the tree.</summary>
    /// <param name="seat">The seat.</param>
    /// <param name="form">Its starting settings.</param>
    /// <param name="controllers">Controller choices: <c>human</c>, <c>llm</c>, and <c>bot:&lt;profile&gt;</c>.</param>
    /// <param name="difficulties">Bot difficulty presets.</param>
    /// <param name="races">Every race in the rules.</param>
    public void Setup(
        Seat seat,
        SeatForm form,
        IReadOnlyList<string> controllers,
        IReadOnlyList<string> difficulties,
        IReadOnlyList<string> races)
    {
        _title.Text = seat.ToString();
        _controllers = controllers;
        _difficulties = [DefaultDifficulty, .. difficulties];
        foreach (string controller in controllers)
        {
            _controller.AddItem(controller);
        }

        foreach (string difficulty in _difficulties)
        {
            _difficulty.AddItem(difficulty);
        }

        string[] parts = form.Controller.Split('@', 2);
        _controller.Select(Math.Max(0, controllers.ToList().IndexOf(parts[0])));
        _difficulty.Select(parts.Length > 1 ? Math.Max(0, _difficulties.ToList().IndexOf(parts[1])) : 0);
        foreach (string race in races)
        {
            CheckBox box = new() { Text = race, ButtonPressed = form.Races?.Contains(race) ?? true };
            box.Toggled += _ => EmitSignal(SignalName.Changed);
            _raceBoxes.Add(box);
            _races.AddChild(box);
        }

        _budget.Value = form.Budget;
        _startingCap.Value = form.StartingCap;
        _controller.ItemSelected += _ => OnControllerChanged();
        _difficulty.ItemSelected += _ => EmitSignal(SignalName.Changed);
        _budget.ValueChanged += _ => EmitSignal(SignalName.Changed);
        _startingCap.ValueChanged += _ => EmitSignal(SignalName.Changed);
        OnControllerChanged();
    }

    private static bool HasDifficulty(string controller) =>
        controller.StartsWith("bot:", StringComparison.Ordinal) && controller != "bot:random";

    private void OnControllerChanged()
    {
        _difficulty.Disabled = !HasDifficulty(_controllers[Math.Max(_controller.Selected, 0)]);
        EmitSignal(SignalName.Changed);
    }
}
