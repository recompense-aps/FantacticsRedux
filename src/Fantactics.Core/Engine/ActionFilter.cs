using Fantactics.Core.Commands;
using Fantactics.Core.Maps;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Core.Engine;

/// <summary>
/// Tells real choices apart from filler, so the UI can skip units with nothing worth doing (decided 2026-09-28: in
/// the LLM playtests about 80% of action prompts only offered Wait, Delay, or an ability with no effect).
/// </summary>
public static class ActionFilter
{
    /// <summary>Whether <paramref name="option"/> can change anything: an attack, or an ability that has an effect.</summary>
    /// <param name="state">The state, with engine ids.</param>
    /// <param name="option">An option from <see cref="LegalActions.For"/> (engine ids).</param>
    public static bool IsMeaningful(GameState state, ActionOption option) => option.Command switch
    {
        Attack => true,
        UseAbility { Ability: AbilityIds.Mend, Target: { } tile } => state.UnitAt(tile) is Unit ally
            && (ally.Hp < state.DefinitionOf(ally).Hp || ally.Has(StatusKind.Slowed) || ally.Has(StatusKind.Rooted)),
        UseAbility { Ability: AbilityIds.Entangle } ability => state.Units.TryGetValue(ability.UnitId, out Unit? druid)
            && state.FieldUnits.Any(enemy => enemy.Owner != druid.Owner
                && state.Map[enemy.Position] == Terrain.Forest
                && enemy.Position.DistanceTo(druid.Position) <= state.Rules.Abilities[AbilityIds.Entangle].MaxRange),
        UseAbility => true,
        _ => false,
    };

    /// <summary>Whether the unit whose slot is up has any meaningful option.</summary>
    public static bool HasMeaningfulAction(GameState state, Seat seat) =>
        LegalActions.For(state, seat) is { Decision: ChooseUnitActionDecision } legal
        && legal.Actions.Any(option => IsMeaningful(state, option));
}
