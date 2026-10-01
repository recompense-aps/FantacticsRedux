using Fantactics.Client.Logic.Board;
using Fantactics.Client.Logic.Input;
using Fantactics.Core.Engine;
using Fantactics.Core.Rules;
using Godot;

namespace Fantactics.Client.Match.Draft;

/// <summary>
/// The draft screen: every unit the seat may draft, grouped by race with its classes and stats, and the army so far
/// split into the starting army and the reserve, with the budget and starting cap. It renders a
/// <see cref="DraftBuilder"/> and reports clicks; the match screen changes the builder and calls <see cref="Show"/>.
/// </summary>
public partial class DraftPanel : PanelContainer
{
    [Export]
    private Label _summary = null!;

    [Export]
    private Label _limits = null!;

    [Export]
    private VBoxContainer _catalog = null!;

    [Export]
    private VBoxContainer _starting = null!;

    [Export]
    private VBoxContainer _reserve = null!;

    [Export]
    private Label _message = null!;

    [Export]
    private Button _suggest = null!;

    [Export]
    private Button _clear = null!;

    [Export]
    private Button _submit = null!;

    /// <summary>Add a unit of <paramref name="type"/> to the starting army or the reserve.</summary>
    [Signal]
    public delegate void AddPressedEventHandler(string type, bool reserve);

    /// <summary>Remove one unit of <paramref name="type"/> from a group.</summary>
    [Signal]
    public delegate void RemovePressedEventHandler(string type, bool reserve);

    /// <summary>Move one unit of <paramref name="type"/> to the other group.</summary>
    [Signal]
    public delegate void MovePressedEventHandler(string type, bool fromReserve);

    /// <summary>Fill the draft with a bot's picks.</summary>
    [Signal]
    public delegate void SuggestPressedEventHandler();

    /// <summary>Empty the draft.</summary>
    [Signal]
    public delegate void ClearPressedEventHandler();

    /// <summary>Submit the draft.</summary>
    [Signal]
    public delegate void SubmitPressedEventHandler();

    /// <inheritdoc />
    public override void _Ready()
    {
        _suggest.Pressed += () => EmitSignal(SignalName.SuggestPressed);
        _clear.Pressed += () => EmitSignal(SignalName.ClearPressed);
        _submit.Pressed += () => EmitSignal(SignalName.SubmitPressed);
    }

    /// <summary>Shows the draft.</summary>
    /// <param name="draft">The draft being built.</param>
    /// <param name="rules">Rules, for unit stats.</param>
    /// <param name="opponent">What's public about the opponent's draft (budget, allowed races).</param>
    /// <param name="message">The last problem to show, if any.</param>
    public void Show(DraftBuilder draft, RulesConfig rules, string opponent, string? message)
    {
        _summary.Text = $"Spent {draft.TotalCost}/{draft.Options.Budget} ({draft.Remaining} left) · "
            + $"Starting army {draft.StartingCost}/{draft.Options.StartingCap}";
        _limits.Text = opponent;
        _message.Text = message ?? string.Join(" ", draft.Problems);
        _submit.Disabled = draft.Problems.Count > 0;

        Clear(_catalog);
        foreach (IGrouping<string, DraftUnitOption> race in draft.Options.Units.GroupBy(unit => unit.Race))
        {
            _catalog.AddChild(new Label { Text = race.Key, ThemeTypeVariation = "HeaderSmall" });
            foreach (DraftUnitOption unit in race.OrderBy(unit => unit.Cost).ThenBy(unit => unit.Type))
            {
                _catalog.AddChild(CatalogRow(draft, unit, rules.Units[unit.Type]));
            }
        }

        FillGroup(_starting, draft, draft.Starting, reserve: false, rules);
        FillGroup(_reserve, draft, draft.Reserve, reserve: true, rules);
    }

    private static void Clear(Container box)
    {
        foreach (Node child in box.GetChildren())
        {
            box.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static Button Button(string text, string? disabledBecause, Action pressed)
    {
        Button button = new()
        {
            Text = text,
            FocusMode = FocusModeEnum.None,
            Disabled = disabledBecause is not null,
            TooltipText = disabledBecause ?? "",
        };
        button.Pressed += pressed;
        return button;
    }

    private HBoxContainer CatalogRow(DraftBuilder draft, DraftUnitOption unit, UnitDefinition stats)
    {
        string features = UnitText.Features(stats);
        HBoxContainer row = new();
        row.AddChild(new Label { Text = unit.Type, CustomMinimumSize = new Vector2(96, 0) });
        row.AddChild(new Label { Text = $"{unit.Cost} pts", CustomMinimumSize = new Vector2(52, 0) });
        Label details = new()
        {
            Text = $"{UnitText.Tags(unit)}\n{UnitText.Stats(stats)}" + (features.Length > 0 ? $" · {features}" : ""),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ThemeTypeVariation = "DimCaption",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        row.AddChild(details);
        row.AddChild(Button(
            "+ Start",
            draft.WhyNot(unit.Type, reserve: false),
            () => EmitSignal(SignalName.AddPressed, unit.Type, false)));
        row.AddChild(Button(
            "+ Reserve",
            draft.WhyNot(unit.Type, reserve: true),
            () => EmitSignal(SignalName.AddPressed, unit.Type, true)));
        return row;
    }

    private void FillGroup(
        VBoxContainer box,
        DraftBuilder draft,
        IReadOnlyList<string> types,
        bool reserve,
        RulesConfig rules)
    {
        Clear(box);
        if (types.Count == 0)
        {
            box.AddChild(new Label { Text = "(none)", ThemeTypeVariation = "DimLabel" });
            return;
        }

        foreach (IGrouping<string, string> group in types.GroupBy(type => type))
        {
            int cost = rules.Units[group.Key].Cost;
            HBoxContainer row = new();
            row.AddChild(new Label
            {
                Text = $"{group.Key} ×{group.Count()} ({cost * group.Count()})",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            });
            string? overCap = reserve && draft.StartingCost + cost > draft.Options.StartingCap
                ? "Over the starting cap."
                : null;
            row.AddChild(Button(
                reserve ? "To start" : "To reserve",
                overCap,
                () => EmitSignal(SignalName.MovePressed, group.Key, reserve)));
            row.AddChild(Button("−", null, () => EmitSignal(SignalName.RemovePressed, group.Key, reserve)));
            box.AddChild(row);
        }
    }
}
