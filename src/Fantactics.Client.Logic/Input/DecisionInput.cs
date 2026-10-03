using System.Globalization;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Geometry;
using Fantactics.Core.Hosting;
using Fantactics.Core.State;

namespace Fantactics.Client.Logic.Input;

/// <summary>
/// Input for the decision one update asks the shown seat for. Holds the builder for that decision (draft, placement,
/// move orders, or an action) and routes clicks, buttons, and keys to it. Input methods return a command when it's
/// ready to submit, or <c>null</c> when the screen only needs to redraw.
/// </summary>
public sealed class DecisionInput
{
    /// <summary>Action key for the "Auto-place" button, which the screen fills through <see cref="Suggest"/>.</summary>
    public const string SuggestAction = "suggest";

    private readonly PlayerView _view;

    /// <summary>Sets up the builder <paramref name="update"/>'s legal actions call for, if any.</summary>
    /// <param name="update">The shown seat's update.</param>
    public DecisionInput(SeatUpdate update)
    {
        _view = update.View;
        Draft = update.Legal is { Decision: DraftArmyDecision, Draft: DraftOptions draft } ? new DraftBuilder(draft) : null;
        Placement = update.Legal is { Decision: PlaceStartingArmyDecision, Placement: PlacementOptions placement }
            ? new PlacementBuilder(update.View, placement)
            : null;
        Moves = update.Legal is { Decision: SubmitMoveOrdersDecision, Moves: MoveOptions moves }
            ? new MoveOrderBuilder(update.View, moves)
            : null;
        Actions = update.Legal is { Decision: ChooseUnitActionDecision decision } legal
            ? new ActionPicker(update.View, decision.UnitId, legal.Actions)
            : null;
    }

    /// <summary>The draft being built, during a draft decision.</summary>
    public DraftBuilder? Draft { get; }

    /// <summary>The starting placement being built, during a placement decision.</summary>
    public PlacementBuilder? Placement { get; }

    /// <summary>The move orders being built, during a move-orders decision.</summary>
    public MoveOrderBuilder? Moves { get; }

    /// <summary>The acting unit's action being picked, during an action decision.</summary>
    public ActionPicker? Actions { get; }

    /// <summary>The last problem input ran into, shown in place of the prompt; <c>null</c> when there is none.</summary>
    public string? Message { get; private set; }

    /// <summary>HUD action buttons: abilities, Wait, Delay, and Auto-place, as (action key, label).</summary>
    public IReadOnlyList<(string Action, string Label)> ActionButtons
    {
        get
        {
            List<(string, string)> buttons = [];
            if (Actions is not null)
            {
                buttons.AddRange(Actions.Abilities.Select((ability, slot) =>
                    (slot.ToString(CultureInfo.InvariantCulture), $"{slot + 1} {ability}")));
                if (Actions.Wait is not null)
                {
                    buttons.Add(("wait", "Wait (W)"));
                }

                if (Actions.Delay is not null)
                {
                    buttons.Add(("delay", "Delay (D)"));
                }
            }

            if (Placement is not null)
            {
                buttons.Add((SuggestAction, "Auto-place"));
            }

            return buttons;
        }
    }

    /// <summary>Roster buttons: starting units to place, or reserve units that can deploy.</summary>
    public IReadOnlyList<(int UnitId, string Label, bool Chosen)> Roster
    {
        get
        {
            if (Placement is PlacementBuilder placement)
            {
                return [.. placement.Options.UnitIds.Select(id => (
                    id,
                    placement.Placed.ContainsKey(id) ? $"{placement.TypeOf(id)} ✓" : placement.TypeOf(id),
                    id == placement.Selected))];
            }

            return Moves is MoveOrderBuilder moves
                ? [.. moves.DeployOptions.Select(d => (d.UnitId, $"{d.Type} ({d.Cost})", moves.Deploys.ContainsKey(d.UnitId)))]
                : [];
        }
    }

    /// <summary>
    /// The label before the roster buttons: <c>Place:</c> during placement, <c>Deploy (Command N):</c> when reserve
    /// units can deploy (N is the Command left after the chosen deploys), otherwise empty.
    /// </summary>
    public string RosterTitle => this switch
    {
        { Placement: not null } => "Place:",
        { Moves: { DeployOptions.IsEmpty: false } moves } => $"Deploy (Command {moves.CommandLeft}):",
        _ => "",
    };

    /// <summary>
    /// What stops the placement or move orders from being submitted (empty when ready), or <c>null</c> when this
    /// decision has no Submit button on the HUD.
    /// </summary>
    public IReadOnlyList<string>? SubmitProblems => Placement?.Problems ?? Moves?.Problems;

    /// <summary>
    /// A click on <paramref name="tile"/>. Placement: left places the picked unit (swapping with one already there)
    /// or picks up a placed unit when none is picked; right takes a placed unit off the board. Moves: left picks a
    /// unit or sends the picked one; right clears a unit's order. Action: left acts on a target; right drops the
    /// picked ability.
    /// </summary>
    /// <returns>The action to submit, when the click chose one.</returns>
    public ICommand? Click(Point tile, bool secondary)
    {
        Message = null;
        if (Placement is not null)
        {
            ClickForPlacement(Placement, tile, secondary);
        }
        else if (Moves is not null)
        {
            Unit? mine = _view.Units.FirstOrDefault(u => u.IsOnField && u.Owner == _view.Seat && u.Position == tile);
            ClickForMoves(Moves, tile, mine, secondary);
        }
        else if (Actions is not null)
        {
            if (secondary)
            {
                Actions.ClearAbility();
            }
            else if (Actions.OptionAt(tile) is ActionOption option)
            {
                return option.Command;
            }
        }

        return null;
    }

    /// <summary>An action button or key: <c>wait</c>, <c>delay</c>, or an ability's 0-based slot.</summary>
    /// <returns>The action to submit, when it needs no target.</returns>
    public ICommand? Action(string action)
    {
        if (Actions is null)
        {
            return null;
        }

        ActionOption? option = action switch
        {
            "wait" => Actions.Wait,
            "delay" => Actions.Delay,
            _ when int.TryParse(action, NumberStyles.None, CultureInfo.InvariantCulture, out int slot) =>
                Actions.SelectAbility(slot),
            _ => null,
        };
        return option?.Command;
    }

    /// <summary>A roster button: picks a unit to place or a reserve unit to deploy.</summary>
    public void ChooseRosterUnit(int unitId)
    {
        if (Placement is not null)
        {
            Placement.Select(unitId);
        }
        else
        {
            Moves?.Select(unitId);
        }
    }

    /// <summary>Changes the draft and keeps any problem the change ran into as the message.</summary>
    /// <param name="change">The change; returns the problem, if any.</param>
    public void ChangeDraft(Func<DraftBuilder, string?> change)
    {
        if (Draft is not null)
        {
            Message = change(Draft);
        }
    }

    /// <summary>Fills the draft or placement with a bot's choice; the player can still change it.</summary>
    /// <param name="suggestion">The bot's draft or placement; anything else is ignored.</param>
    public void Suggest(ICommand? suggestion)
    {
        switch (suggestion)
        {
            case SubmitDraft draft:
                Draft?.Load(draft);
                break;
            case PlaceStartingArmy placement:
                Placement?.Load(placement);
                break;
        }

        Message = null;
    }

    /// <summary>The finished draft, placement, or move orders.</summary>
    /// <returns>The command, or <c>null</c> when there is nothing to submit or it still has problems.</returns>
    public ICommand? Submit() => this switch
    {
        { Draft: { Problems.Count: 0 } draft } => draft.Build(),
        { Placement: { Problems.Count: 0 } placement } => placement.Build(),
        { Moves: { Problems.Count: 0 } moves } => moves.Build(),
        _ => null,
    };

    /// <summary>Drops the picked unit or ability.</summary>
    /// <returns>Whether there was a pick to drop.</returns>
    public bool Cancel()
    {
        if (Moves?.Selected is null && Actions?.Ability is null && Placement?.Selected is null)
        {
            return false;
        }

        Moves?.Deselect();
        Actions?.ClearAbility();
        Placement?.Deselect();
        return true;
    }

    /// <summary>Shows why the engine rejected a submitted command.</summary>
    public void Reject(RuleViolation violation) => Message = violation.Message;

    private void ClickForPlacement(PlacementBuilder placement, Point tile, bool secondary)
    {
        int? there = placement.UnitAt(tile);
        if (secondary)
        {
            if (there is int unit)
            {
                placement.Clear(unit);
            }
        }
        else if (placement.Selected is null && there is int unit)
        {
            placement.Select(unit);
        }
        else
        {
            Message = placement.Choose(tile);
        }
    }

    private void ClickForMoves(MoveOrderBuilder moves, Point tile, Unit? mine, bool secondary)
    {
        if (secondary)
        {
            int? arriving = moves.Deploys.FirstOrDefault(d => d.Value == tile).Key;
            if (mine is not null || arriving is > 0)
            {
                moves.Clear(mine?.Id ?? arriving ?? 0);
            }

            moves.Deselect();
        }
        else if (moves.Selected is not null && moves.Choose(tile) is string problem)
        {
            if (mine is null || !moves.Select(mine.Id))
            {
                Message = problem;
            }
        }
        else if (moves.Selected is null && mine is not null && !moves.Select(mine.Id))
        {
            Message = $"{mine.Type} can't move this turn.";
        }
    }
}
