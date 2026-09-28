using System.Collections.Immutable;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Geometry;
using Fantactics.Core.State;

namespace Fantactics.Client.Logic.Input;

/// <summary>
/// Turns clicks into the acting unit's action: clicking an enemy in range attacks it; picking an ability (from the
/// bar or hotkeys 1–9) switches clicks to that ability's targets. Only listed options can be picked, so every
/// action it returns is legal.
/// </summary>
public sealed class ActionPicker
{
    private readonly PlayerView _view;
    private readonly ImmutableArray<ActionOption> _options;

    /// <summary>Starts with no ability selected (clicks attack).</summary>
    /// <param name="view">The seat's view.</param>
    /// <param name="unitId">The acting unit.</param>
    /// <param name="options">Its legal actions.</param>
    public ActionPicker(PlayerView view, int unitId, ImmutableArray<ActionOption> options)
    {
        _view = view;
        UnitId = unitId;
        _options = options;
        Abilities = [.. options.Select(o => o.Command).OfType<UseAbility>().Select(a => a.Ability).Distinct()];
    }

    /// <summary>The acting unit.</summary>
    public int UnitId { get; }

    /// <summary>The unit's usable abilities, in hotkey order.</summary>
    public ImmutableArray<string> Abilities { get; }

    /// <summary>The selected ability, or <c>null</c> when clicks attack.</summary>
    public string? Ability { get; private set; }

    /// <summary>The Wait option, if listed.</summary>
    public ActionOption? Wait => _options.FirstOrDefault(o => o.Command is Wait);

    /// <summary>The Delay option, if listed.</summary>
    public ActionOption? Delay => _options.FirstOrDefault(o => o.Command is Delay);

    /// <summary>Tiles a click would act on: enemies in range, or the selected ability's targets.</summary>
    public IEnumerable<Point> Targets => _options
        .Select(TargetOf)
        .OfType<Point>()
        .Distinct();

    /// <summary>
    /// Selects an ability by hotkey slot (0-based). An ability with a single untargeted option is returned at once,
    /// ready to submit.
    /// </summary>
    /// <returns>The option to submit right away, if the ability needs no target.</returns>
    public ActionOption? SelectAbility(int slot)
    {
        if (slot < 0 || slot >= Abilities.Length)
        {
            return null;
        }

        Ability = Abilities[slot];
        ActionOption[] uses = [.. _options.Where(o => o.Command is UseAbility use && use.Ability == Ability)];
        return uses is [{ Command: UseAbility { Target: null } } only] ? only : null;
    }

    /// <summary>Goes back to attacking on click.</summary>
    public void ClearAbility() => Ability = null;

    /// <summary>The option a click on <paramref name="tile"/> picks, if any.</summary>
    public ActionOption? OptionAt(Point tile) => _options.FirstOrDefault(option => TargetOf(option) == tile);

    private Point? TargetOf(ActionOption option) => (option.Command, Ability) switch
    {
        (Attack attack, null) => _view.Units.FirstOrDefault(u => u.Id == attack.TargetId)?.Position,
        (UseAbility use, string ability) when use.Ability == ability => use.Target
            ?? _view.Units.FirstOrDefault(u => u.Id == UnitId)?.Position,
        _ => null,
    };
}
