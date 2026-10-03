using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Geometry;
using Fantactics.Core.Hosting;
using Fantactics.Core.State;

namespace Fantactics.Client.Logic.Drive;

/// <summary>
/// The clicks and key presses a person would make to give a decision's answer, for <c>--drive</c>. Plans go through
/// the same buttons, tiles, and InputMap actions as play, so a broken scene connection stops the run. Button texts
/// here are the ones the scenes show.
/// </summary>
public static class InputPlan
{
    /// <summary>The draft panel's suggest button.</summary>
    public const string BotPick = "Bot pick";

    /// <summary>The draft panel's clear button.</summary>
    public const string ClearDraft = "Clear";

    /// <summary>The draft panel's submit button.</summary>
    public const string SubmitDraft = "Submit draft (Enter)";

    /// <summary>The HUD's submit button (placement and move orders).</summary>
    public const string Submit = "Submit (Enter)";

    /// <summary>The HUD's placement suggest button.</summary>
    public const string AutoPlace = "Auto-place";

    /// <summary>The most abilities that have a hotkey (<c>ability_1</c> to <c>ability_9</c>).</summary>
    private const int AbilityKeys = 9;

    /// <summary>The menu route from <paramref name="menu"/> (<c>main</c> or <c>new</c>) to a new match's draft.</summary>
    public static IReadOnlyList<InputStep> Menu(string menu) => menu == "new"
        ? [InputStep.Shot("new-match"), InputStep.Button("Start")]
        : [InputStep.Shot("main-menu"), InputStep.Button("New match…"), .. Menu("new")];

    /// <summary>A short name for the decision <paramref name="update"/> asks for (for messages and screenshots).</summary>
    public static string DecisionName(SeatUpdate update) => update.Legal?.Decision switch
    {
        DraftArmyDecision => "draft",
        PlaceStartingArmyDecision => "placement",
        SubmitMoveOrdersDecision => "orders",
        ChooseUnitActionDecision => "action",
        _ => "none",
    };

    /// <summary>The steps that answer <paramref name="update"/>'s decision.</summary>
    /// <param name="update">The shown seat's update; it must ask for a decision.</param>
    /// <param name="choice">
    /// The answer to give (a bot's), in the seat's own ids. A draft ignores it and uses the panel's Bot pick; a
    /// placement places it by hand, then swaps to Auto-place.
    /// </param>
    /// <exception cref="ArgumentException">The choice doesn't answer the update's decision.</exception>
    public static IReadOnlyList<InputStep> For(SeatUpdate update, ICommand choice) =>
        (update.Legal, choice) switch
        {
            ({ Decision: DraftArmyDecision }, _) => DraftSteps(),
            ({ Decision: PlaceStartingArmyDecision }, PlaceStartingArmy placement) =>
                PlacementSteps(update.View, placement),
            ({ Decision: SubmitMoveOrdersDecision, Moves: MoveOptions options }, SubmitMoveOrders orders) =>
                OrderSteps(update.View, options, orders),
            ({ Decision: ChooseUnitActionDecision } legal, IUnitActionCommand action) =>
                ActionSteps(update.View, legal, action),
            _ => throw new ArgumentException(
                $"{choice.GetType().Name} doesn't answer a {DecisionName(update)} decision.", nameof(choice)),
        };

    private static IReadOnlyList<InputStep> DraftSteps() =>
    [
        InputStep.Shot("draft"),
        InputStep.Button(BotPick),
        InputStep.Button(ClearDraft),
        InputStep.Button(BotPick),
        InputStep.Shot("draft-picked"),
        InputStep.Button(SubmitDraft),
    ];

    /// <summary>
    /// Places every unit by roster button and tile click, takes the first back off with a right click, then lets
    /// Auto-place fill the board and submits with the key.
    /// </summary>
    private static IReadOnlyList<InputStep> PlacementSteps(PlayerView view, PlaceStartingArmy placement)
    {
        List<InputStep> steps = [InputStep.Shot("placement")];
        foreach (UnitPlacement place in placement.Placements)
        {
            steps.Add(InputStep.Button(TypeOf(view, place.UnitId)));
            steps.Add(InputStep.Click(place.Tile));
        }

        steps.Add(InputStep.Shot("placement-placed"));
        if (placement.Placements is [UnitPlacement first, ..])
        {
            steps.Add(InputStep.Click(first.Tile, secondary: true));
        }

        steps.Add(InputStep.Button(AutoPlace));
        steps.Add(InputStep.Press("submit"));
        return steps;
    }

    /// <summary>
    /// Picks the first moving unit and drops it with Esc, then gives every order by clicking the unit and its
    /// destination (deploys: roster button, then arrival tile), and submits with the button.
    /// </summary>
    private static IReadOnlyList<InputStep> OrderSteps(PlayerView view, MoveOptions options, SubmitMoveOrders orders)
    {
        MoveOrder[] moves = [.. orders.Moves.Where(move => move.Path.Length > 0 && From(view, move) != move.Path[^1])];
        List<InputStep> steps = [InputStep.Shot("orders")];
        if (moves is [MoveOrder first, ..])
        {
            steps.Add(InputStep.Click(From(view, first)));
            steps.Add(InputStep.Press("cancel"));
        }

        foreach (MoveOrder move in moves)
        {
            steps.Add(InputStep.Click(From(view, move)));
            steps.Add(InputStep.Click(move.Path[^1]));
        }

        foreach (DeployOrder deploy in orders.Deploys)
        {
            DeployOption option = options.Deploys.FirstOrDefault(d => d.UnitId == deploy.UnitId)
                ?? throw new ArgumentException($"Unit {deploy.UnitId} can't deploy.", nameof(orders));
            steps.Add(InputStep.Button($"{option.Type} ({option.Cost})"));
            steps.Add(InputStep.Click(deploy.Tile));
        }

        steps.Add(InputStep.Shot("orders-given"));
        steps.Add(InputStep.Button(Submit));
        return steps;
    }

    /// <summary>Wait and Delay by key; an ability by its hotkey, then its target; an attack by clicking the enemy.</summary>
    private static IReadOnlyList<InputStep> ActionSteps(PlayerView view, LegalActions legal, IUnitActionCommand action)
    {
        List<InputStep> steps = [InputStep.Shot("action")];
        switch (action)
        {
            case Wait:
                steps.Add(InputStep.Press("wait_action"));
                break;
            case Delay:
                steps.Add(InputStep.Press("delay_action"));
                break;
            case Attack attack:
                steps.Add(InputStep.Click(PositionOf(view, attack.TargetId)));
                break;
            case UseAbility use:
                string[] abilities = [.. legal.Actions
                    .Select(option => option.Command)
                    .OfType<UseAbility>()
                    .Select(a => a.Ability)
                    .Distinct()];
                int slot = Array.IndexOf(abilities, use.Ability);
                steps.Add(slot < AbilityKeys
                    ? InputStep.Press($"ability_{slot + 1}")
                    : InputStep.Button($"{slot + 1} {use.Ability}"));
                UseAbility[] uses = [.. legal.Actions
                    .Select(option => option.Command)
                    .OfType<UseAbility>()
                    .Where(a => a.Ability == use.Ability)];
                if (uses is not [{ Target: null }])
                {
                    steps.Add(InputStep.Click(use.Target ?? PositionOf(view, use.UnitId)));
                }

                break;
            default:
                throw new ArgumentException($"No input gives a {action.GetType().Name}.", nameof(action));
        }

        return steps;
    }

    private static Point From(PlayerView view, MoveOrder move) => PositionOf(view, move.UnitId);

    private static Point PositionOf(PlayerView view, int unitId) =>
        view.Units.FirstOrDefault(u => u.Id == unitId)?.Position
            ?? throw new ArgumentException($"Unit {unitId} has no position in the view.");

    private static string TypeOf(PlayerView view, int unitId) =>
        view.Units.FirstOrDefault(u => u.Id == unitId)?.Type
            ?? throw new ArgumentException($"Unit {unitId} isn't in the view.");
}
