using Fantactics.Core.Events;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Core.Engine;

/// <summary>Deterministic combat (GameDesign §4.3): damage, strikes, healing, statuses, and deaths.</summary>
public static class CombatRules
{
    /// <summary>
    /// Damage <paramref name="attacker"/> deals to <paramref name="target"/>:
    /// <c>max(1, Attack + Support − (Defense + terrain Defense))</c>, with point-blank and clash halving applied to
    /// Attack before Support is added.
    /// </summary>
    public static int Damage(GameState state, Unit attacker, Unit target, AttackKind kind)
    {
        UnitDefinition definition = state.DefinitionOf(attacker);
        int attack = kind switch
        {
            AttackKind.PointBlank => definition.Attack / 2,
            AttackKind.Clash => (definition.IsRanged ? definition.Attack / 2 : definition.Attack)
                + UnitRules.Trait(state, attacker, TraitIds.Reckless),
            AttackKind.Retaliate => definition.IsRanged ? definition.Attack / 2 : definition.Attack,
            _ => definition.Attack,
        };

        int terrainDefense = UnitRules.TerrainDefense(state, target.Position);
        if (terrainDefense > 0 && UnitRules.HasTrait(state, attacker, TraitIds.Crush))
        {
            terrainDefense = 0;
        }

        int defense = state.DefinitionOf(target).Defense + terrainDefense;
        return Math.Max(1, attack + Support(state, attacker, target, kind) - defense);
    }

    /// <summary>
    /// +1 per other friendly unit of the attacker adjacent to the target, up to the cap. With
    /// <see cref="RulesConfig.SupportMeleeOnly"/>, ranged strikes (basic attacks from 2+ tiles) get none.
    /// </summary>
    public static int Support(GameState state, Unit attacker, Unit target, AttackKind kind)
    {
        bool ranged = kind == AttackKind.Basic && attacker.Position.DistanceTo(target.Position) > 1;
        if (ranged && state.Rules.SupportMeleeOnly)
        {
            return 0;
        }

        return Math.Min(
            state.Rules.SupportCap,
            state.FieldUnits.Count(unit =>
                !state.AreEnemies(unit, attacker)
                && unit.Id != attacker.Id
                && unit.Position.IsAdjacentTo(target.Position)));
    }

    /// <summary>
    /// The kind of basic attack <paramref name="attacker"/> can make on <paramref name="target"/>, or <c>null</c> if
    /// the target is out of range or out of line of sight.
    /// </summary>
    public static AttackKind? BasicAttackKind(GameState state, Unit attacker, Unit target)
    {
        UnitDefinition definition = state.DefinitionOf(attacker);
        int distance = attacker.Position.DistanceTo(target.Position);
        if (!state.AreEnemies(target, attacker) || distance < definition.MinRange || distance > definition.MaxRange)
        {
            return null;
        }

        if (distance >= 2 && !UnitRules.HasLineOfSight(state, attacker, target.Position))
        {
            return null;
        }

        return distance == 1 && definition.IsRanged ? AttackKind.PointBlank : AttackKind.Basic;
    }

    /// <summary>Predicts a basic attack's damage and whether it kills.</summary>
    public static AttackPreview Preview(GameState state, Unit attacker, Unit target, AttackKind kind)
    {
        int damage = Damage(state, attacker, target, kind);
        return new AttackPreview(target.Id, damage, damage >= target.Hp);
    }

    /// <summary>
    /// Heal amount after the unit's attack deals damage: Bloodthirst plus 1 inside the War Cry of another unit of its
    /// race (race auras only reach their own race, RacesAndUnits §2.4).
    /// </summary>
    public static int EffectiveBloodthirst(GameState state, Unit unit)
    {
        bool inWarCry = state.FieldUnits.Any(other =>
            !state.AreEnemies(other, unit)
            && other.Id != unit.Id
            && UnitRules.SharesRace(state, other, unit)
            && UnitRules.Trait(state, other, TraitIds.WarCry) is int radius and > 0
            && other.Position.DistanceTo(unit.Position) <= radius);
        return UnitRules.Trait(state, unit, TraitIds.Bloodthirst) + (inWarCry ? 1 : 0);
    }

    /// <summary>
    /// One strike: damage, death, and on-hit traits (Hamstring on the target, Bloodthirst on the attacker).
    /// Clash strikes skip on-hit traits: clashes are pure fighting (GameDesign §4.3).
    /// </summary>
    internal static GameState Strike(GameState state, int attackerId, int targetId, AttackKind kind, List<GameEvent> events)
    {
        Unit attacker = state.Units[attackerId];
        Unit target = state.Units[targetId];
        int damage = Damage(state, attacker, target, kind);
        int hp = target.Hp - damage;
        events.Add(new UnitAttacked(attackerId, targetId, kind, damage, Math.Max(0, hp)));

        state = hp <= 0 ? Kill(state, target, attacker, events) : state.WithUnit(target with { Hp = hp });
        if (kind == AttackKind.Clash)
        {
            return state;
        }

        int hamstring = UnitRules.Trait(state, attacker, TraitIds.Hamstring);
        if (hamstring > 0 && state.Units.ContainsKey(targetId))
        {
            state = ApplyStatus(state, targetId, StatusKind.Slowed, hamstring, events);
        }

        int bloodthirst = EffectiveBloodthirst(state, attacker);
        return bloodthirst > 0 ? Heal(state, attackerId, bloodthirst, events) : state;
    }

    /// <summary>Heals up to max HP; emits an event only if HP actually changed.</summary>
    internal static GameState Heal(GameState state, int unitId, int amount, List<GameEvent> events)
    {
        Unit unit = state.Units[unitId];
        int healed = Math.Min(amount, state.DefinitionOf(unit).Hp - unit.Hp);
        if (healed <= 0)
        {
            return state;
        }

        events.Add(new UnitHealed(unitId, healed, unit.Hp + healed));
        return state.WithUnit(unit with { Hp = unit.Hp + healed });
    }

    /// <summary>Applies or refreshes a status lasting <paramref name="duration"/> turns after this one.</summary>
    internal static GameState ApplyStatus(GameState state, int unitId, StatusKind status, int duration, List<GameEvent> events)
    {
        Unit unit = state.Units[unitId];
        int lastsThrough = state.Turn + duration;
        events.Add(new StatusApplied(unitId, status, lastsThrough));
        return state.WithUnit(unit with { Statuses = unit.Statuses.SetItem(status, lastsThrough) });
    }

    /// <summary>Removes a status if present.</summary>
    internal static GameState RemoveStatus(GameState state, int unitId, StatusKind status, List<GameEvent> events)
    {
        Unit unit = state.Units[unitId];
        if (!unit.Has(status))
        {
            return state;
        }

        events.Add(new StatusRemoved(unitId, status));
        return state.WithUnit(unit with { Statuses = unit.Statuses.Remove(status) });
    }

    private static GameState Kill(GameState state, Unit victim, Unit killer, List<GameEvent> events)
    {
        int value = victim.IsSummoned ? 0 : state.DefinitionOf(victim).Cost;
        PlayerState credited = state.Players[killer.Owner];
        events.Add(new UnitDied(victim.Id, killer.Id, value));
        return state.WithPlayer(credited with { DestroyedValue = credited.DestroyedValue + value }) with
        {
            Units = state.Units.Remove(victim.Id),
            TurnState = state.TurnState.WithoutActor(victim.Id),
        };
    }
}
