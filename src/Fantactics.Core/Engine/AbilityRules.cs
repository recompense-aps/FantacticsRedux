using Fantactics.Core.Commands;
using Fantactics.Core.Events;
using Fantactics.Core.Geometry;
using Fantactics.Core.Maps;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Core.Engine;

/// <summary>MVP ability behavior (RacesAndUnits §3.2, §4.2). Numbers come from <see cref="AbilityDefinition"/>.</summary>
internal static class AbilityRules
{
    private const string SummonedType = "Grunt";

    private static readonly HashSet<Terrain> _overgrowable = [Terrain.Plains, Terrain.Road, Terrain.Hills];

    /// <summary>Validates and applies an ability, then starts its cooldown.</summary>
    public static GameState Apply(GameState state, Unit unit, UseAbility command, List<GameEvent> events)
    {
        RuleViolationException.ThrowUnless(
            state.DefinitionOf(unit).Abilities.Contains(command.Ability),
            "unknown-ability",
            $"Unit {unit.Id} doesn't have {command.Ability}.");
        int readyTurn = unit.AbilityReadyTurn.GetValueOrDefault(command.Ability);
        RuleViolationException.ThrowUnless(
            readyTurn <= state.Turn,
            "on-cooldown",
            $"{command.Ability} is ready again on turn {readyTurn}.");

        bool untargeted = IsUntargeted(command.Ability);
        RuleViolationException.ThrowUnless(
            (command.Target is null) == untargeted,
            "bad-target",
            untargeted ? $"{command.Ability} doesn't take a target." : $"{command.Ability} needs a target tile.");
        Point target = command.Target ?? unit.Position;

        AbilityDefinition ability = state.Rules.Abilities[command.Ability];
        events.Add(new AbilityUsed(unit.Id, command.Ability, command.Target));
        state = command.Ability switch
        {
            AbilityIds.PinningShot => PinningShot(state, unit, target, ability, events),
            AbilityIds.Mend => Mend(state, unit, target, ability, events),
            AbilityIds.Overgrowth => Overgrowth(state, unit, target, ability, events),
            AbilityIds.Entangle => Entangle(state, unit, ability, events),
            AbilityIds.ThrowNet => ThrowNet(state, unit, target, ability, events),
            AbilityIds.CallTheHorde => CallTheHorde(state, unit, ability, events),
            _ => throw new RuleViolationException("unknown-ability", $"{command.Ability} has no implementation."),
        };

        // The user can die to Retaliate during its own ability.
        if (ability.Cooldown > 0 && state.Units.TryGetValue(unit.Id, out Unit? after))
        {
            int ready = state.Turn + ability.Cooldown + 1;
            state = state.WithUnit(after with { AbilityReadyTurn = after.AbilityReadyTurn.SetItem(command.Ability, ready) });
        }

        return state;
    }

    /// <summary>Whether the ability takes no target tile.</summary>
    public static bool IsUntargeted(string ability) => ability is AbilityIds.Entangle or AbilityIds.CallTheHorde;

    private static GameState PinningShot(
        GameState state, Unit unit, Point target, AbilityDefinition ability, List<GameEvent> events)
    {
        Unit enemy = RequireEnemyAt(state, unit, target);
        AttackKind kind = CombatRules.BasicAttackKind(state, unit, enemy)
            ?? throw new RuleViolationException("out-of-range", $"Unit {enemy.Id} is out of range or out of sight.");
        state = ActionRules.AttackWithRetaliation(state, unit.Id, enemy.Id, kind, events);
        return state.Units.ContainsKey(enemy.Id)
            ? CombatRules.ApplyStatus(state, enemy.Id, StatusKind.Rooted, ability.Duration, events)
            : state;
    }

    private static GameState Mend(GameState state, Unit unit, Point target, AbilityDefinition ability, List<GameEvent> events)
    {
        Unit? ally = state.UnitAt(target);
        RuleViolationException.ThrowUnless(
            ally is not null && !state.AreEnemies(ally, unit) && ally.Id != unit.Id,
            "bad-target",
            $"Mend needs another friendly unit on {target}.");
        RuleViolationException.ThrowUnless(
            ability.AllyScope == AllyScope.AnyFriendly || UnitRules.SharesRace(state, unit, ally),
            "bad-target",
            $"Mend only heals units that share the Herbalist's race; unit {ally.Id} doesn't.");
        RequireInRange(unit, target, ability);
        state = CombatRules.Heal(state, ally.Id, ability.Amount, events);
        state = CombatRules.RemoveStatus(state, ally.Id, StatusKind.Slowed, events);
        return CombatRules.RemoveStatus(state, ally.Id, StatusKind.Rooted, events);
    }

    private static GameState Overgrowth(
        GameState state, Unit unit, Point target, AbilityDefinition ability, List<GameEvent> events)
    {
        RuleViolationException.ThrowUnless(state.Map.Contains(target), "bad-target", $"{target} is off the map.");
        RequireInRange(unit, target, ability);
        RuleViolationException.ThrowUnless(
            _overgrowable.Contains(state.Map[target]),
            "bad-target",
            $"Overgrowth only works on Plains, Road, or Hills ({target} is {state.Map[target]}).");
        events.Add(new TileChanged(target, Terrain.Forest));
        return state with { Map = state.Map.WithTerrain(target, Terrain.Forest) };
    }

    private static GameState Entangle(GameState state, Unit unit, AbilityDefinition ability, List<GameEvent> events)
    {
        List<int> targets = state.FieldUnits
            .Where(enemy => state.AreEnemies(enemy, unit)
                && state.Map[enemy.Position] == Terrain.Forest
                && enemy.Position.DistanceTo(unit.Position) <= ability.MaxRange)
            .Select(enemy => enemy.Id)
            .ToList();
        return targets.Aggregate(
            state,
            (current, id) => CombatRules.ApplyStatus(current, id, StatusKind.Rooted, ability.Duration, events));
    }

    private static GameState ThrowNet(
        GameState state, Unit unit, Point target, AbilityDefinition ability, List<GameEvent> events)
    {
        Unit enemy = RequireEnemyAt(state, unit, target);
        RequireInRange(unit, target, ability);
        RuleViolationException.ThrowUnless(
            !ability.NeedsLineOfSight || UnitRules.HasLineOfSight(state, unit, target),
            "no-line-of-sight",
            $"No line of sight to {target}.");
        return CombatRules.ApplyStatus(state, enemy.Id, StatusKind.Rooted, ability.Duration, events);
    }

    private static GameState CallTheHorde(GameState state, Unit unit, AbilityDefinition ability, List<GameEvent> events)
    {
        int alive = state.FieldUnits.Count(other => other.Owner == unit.Owner && other.IsSummoned);
        List<Point> free = unit.Position.Neighbors()
            .Where(tile => UnitRules.IsPassable(state, tile) && state.UnitAt(tile) is null)
            .Take(Math.Min(ability.Amount, ability.Limit - alive))
            .ToList();
        RuleViolationException.ThrowUnless(
            free.Count > 0,
            "no-room",
            alive >= ability.Limit
                ? $"At most {ability.Limit} summoned units can be alive at once."
                : "No free tile next to the caster.");

        foreach (Point tile in free)
        {
            int id = state.NextUnitId;
            int hp = state.Rules.Units[SummonedType].Hp;
            Unit summoned = Unit.Create(id, unit.Owner, SummonedType, UnitLocation.Field, tile, hp);
            state = state.WithUnit(summoned with { IsSummoned = true, CannotActOnTurn = state.Turn }) with
            {
                NextUnitId = id + 1,
            };
            events.Add(new UnitSummoned(id, unit.Owner, SummonedType, tile));
        }

        return state;
    }

    private static Unit RequireEnemyAt(GameState state, Unit unit, Point target)
    {
        Unit? enemy = state.UnitAt(target);
        return enemy is not null && state.AreEnemies(enemy, unit)
            ? enemy
            : throw new RuleViolationException("bad-target", $"No enemy on {target}.");
    }

    private static void RequireInRange(Unit unit, Point target, AbilityDefinition ability)
    {
        int distance = unit.Position.DistanceTo(target);
        RuleViolationException.ThrowUnless(
            distance >= ability.MinRange && distance <= ability.MaxRange,
            "out-of-range",
            $"{target} is {distance} tiles away; the range is {ability.MinRange}-{ability.MaxRange}.");
    }
}
