using Fantactics.Core.Commands;
using Fantactics.Core.Events;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Core.Engine;

/// <summary>The action phase: one action per unit in initiative order (GameDesign §4.2).</summary>
internal static class ActionRules
{
    /// <summary>Validates and applies an action for the unit whose slot is up, then advances the order.</summary>
    public static GameState Apply(GameState state, Seat seat, IUnitActionCommand command, List<GameEvent> events)
    {
        RuleViolationException.ThrowUnless(
            state.Phase == Phase.Action && state.TurnState.CurrentActor == command.UnitId,
            "not-your-decision",
            $"It's not unit {command.UnitId}'s turn to act.");
        Unit unit = state.Units[command.UnitId];
        RuleViolationException.ThrowUnless(unit.Owner == seat, "not-your-decision", $"Unit {unit.Id} isn't yours.");

        switch (command)
        {
            case Delay:
                RuleViolationException.ThrowUnless(
                    !state.TurnState.HasDelayed.Contains(unit.Id),
                    "already-delayed",
                    $"Unit {unit.Id} already delayed this turn.");
                events.Add(new UnitDelayed(unit.Id));
                return state with
                {
                    TurnState = state.TurnState with
                    {
                        ActionQueue = state.TurnState.ActionQueue.Remove(unit.Id),
                        DelayedQueue = state.TurnState.DelayedQueue.Add(unit.Id),
                        HasDelayed = state.TurnState.HasDelayed.Add(unit.Id),
                    },
                };
            case Wait:
                events.Add(new UnitWaited(unit.Id));
                break;
            case Attack attack:
                state = ApplyAttack(state, unit, attack, events);
                break;
            case UseAbility ability:
                state = AbilityRules.Apply(state, unit, ability, events);
                break;
            default:
                throw new RuleViolationException("unknown-command", $"{command.GetType().Name} is not an action.");
        }

        return Finish(state, unit.Id, events);
    }

    /// <summary>A strike followed by the target's Retaliate, if it survives and was hit from distance 1.</summary>
    internal static GameState AttackWithRetaliation(
        GameState state, int attackerId, int targetId, AttackKind kind, List<GameEvent> events)
    {
        int distance = state.Units[attackerId].Position.DistanceTo(state.Units[targetId].Position);
        state = CombatRules.Strike(state, attackerId, targetId, kind, events);

        if (distance != 1
            || !state.Units.TryGetValue(targetId, out Unit? target)
            || !state.Units.ContainsKey(attackerId)
            || !UnitRules.HasTrait(state, target, TraitIds.Retaliate)
            || state.TurnState.RetaliateUsed.Contains(targetId)
            || state.DefinitionOf(target).MinRange > 1)
        {
            return state;
        }

        state = state with
        {
            TurnState = state.TurnState with { RetaliateUsed = state.TurnState.RetaliateUsed.Add(targetId) },
        };
        return CombatRules.Strike(state, targetId, attackerId, AttackKind.Retaliate, events);
    }

    private static GameState ApplyAttack(GameState state, Unit unit, Attack attack, List<GameEvent> events)
    {
        RuleViolationException.ThrowUnless(
            state.Units.TryGetValue(attack.TargetId, out Unit? target) && target.IsOnField,
            "bad-target",
            $"Unit {attack.TargetId} is not on the field.");
        AttackKind kind = CombatRules.BasicAttackKind(state, unit, target)
            ?? throw new RuleViolationException(
                "out-of-range",
                $"Unit {target.Id} is not an enemy in range and line of sight of unit {unit.Id}.");
        return AttackWithRetaliation(state, unit.Id, target.Id, kind, events);
    }

    private static GameState Finish(GameState state, int unitId, List<GameEvent> events)
    {
        state = state with { TurnState = state.TurnState.WithoutActor(unitId) };
        return state.TurnState.CurrentActor is null ? TurnRules.EndTurn(state, events) : state;
    }
}
