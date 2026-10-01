using System.Collections.Immutable;
using Fantactics.Core.Commands;
using Fantactics.Core.Events;
using Fantactics.Core.Geometry;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Core.Engine;

/// <summary>
/// The options for a seat's pending decision (Simulation §2). Bots, LLMs, and the UI pick from these; every listed
/// option is accepted by <see cref="GameEngine.Apply"/>. Move options are per unit: the joint move space is never
/// enumerated, and joint constraints are checked when the orders are submitted.
/// </summary>
/// <param name="Decision">The decision these options answer.</param>
/// <param name="Draft">Draft options, for <see cref="DraftArmyDecision"/>.</param>
/// <param name="Placement">Placement options, for <see cref="PlaceStartingArmyDecision"/>.</param>
/// <param name="Moves">Move and deploy options, for <see cref="SubmitMoveOrdersDecision"/>.</param>
/// <param name="Actions">Complete action commands, for <see cref="ChooseUnitActionDecision"/>.</param>
public sealed record LegalActions(
    Decision Decision,
    DraftOptions? Draft,
    PlacementOptions? Placement,
    MoveOptions? Moves,
    ImmutableArray<ActionOption> Actions)
{
    /// <summary>Options for the decision <paramref name="seat"/> owes, or <c>null</c> if it owes none.</summary>
    /// <remarks>Uses only information visible to <paramref name="seat"/>.</remarks>
    public static LegalActions? For(GameState state, Seat seat) =>
        GameEngine.PendingDecisionFor(state, seat) switch
        {
            DraftArmyDecision decision => new(decision, DraftOptionsFor(state, seat), null, null, []),
            PlaceStartingArmyDecision decision => new(decision, null, PlacementOptionsFor(state, seat), null, []),
            SubmitMoveOrdersDecision decision => new(decision, null, null, MoveOptionsFor(state, seat), []),
            ChooseUnitActionDecision decision => new(decision, null, null, null, ActionOptionsFor(state, decision.UnitId)),
            _ => null,
        };

    /// <summary>
    /// Like <see cref="For"/>, but with <paramref name="seat"/>'s own unit ids (<see cref="ViewIds"/>), to go with
    /// <see cref="PlayerView.Project"/>. Translate a chosen command back with <see cref="ViewIds.ToEngine(ICommand)"/>.
    /// </summary>
    public static LegalActions? ForView(GameState state, Seat seat) =>
        For(state, seat) is LegalActions legal ? ViewIds.For(state, seat).ToView(legal) : null;

    private static DraftOptions DraftOptionsFor(GameState state, Seat seat)
    {
        PlayerState player = state.Players[seat];
        ImmutableArray<DraftUnitOption> units = state.Rules.Units
            .Where(pair => player.MayDraft(pair.Value.Race))
            .Select(pair => new DraftUnitOption(
                pair.Key,
                pair.Value.Cost,
                pair.Value.Unique,
                pair.Value.Race,
                pair.Value.Classes ?? []))
            .ToImmutableArray();
        return new DraftOptions(units, player.BudgetUnder(state.Rules), player.StartingCapUnder(state.Rules));
    }

    private static PlacementOptions PlacementOptionsFor(GameState state, Seat seat)
    {
        ImmutableArray<int> unitIds = state.Units.Values
            .Where(unit => unit.Owner == seat && unit.Location == UnitLocation.Unplaced)
            .Select(unit => unit.Id)
            .ToImmutableArray();
        ImmutableArray<Point> tiles = state.Map.AllPoints()
            .Where(tile => state.Map.IsInDeployZone(tile, seat, state.Rules.DeployColumns)
                && UnitRules.IsPassable(state, tile))
            .ToImmutableArray();
        return new PlacementOptions(unitIds, tiles);
    }

    private static MoveOptions MoveOptionsFor(GameState state, Seat seat)
    {
        ImmutableArray<UnitMoveOptions> units = state.FieldUnits
            .Where(unit => unit.Owner == seat)
            .Select(unit => new UnitMoveOptions(unit.Id, Pathfinder.Reachable(state, unit)))
            .ToImmutableArray();
        int command = state.Players[seat].Command;
        ImmutableArray<DeployOption> deploys = state.Units.Values
            .Where(unit => unit.Owner == seat
                && unit.Location == UnitLocation.Reserve
                && UnitRules.DeployCost(state, unit) <= command)
            .Select(unit => new DeployOption(
                unit.Id,
                unit.Type,
                UnitRules.DeployCost(state, unit),
                state.Map.AllPoints()
                    .Where(tile => MoveOrderRules.IsDeployTile(state, seat, unit, tile))
                    .ToImmutableArray()))
            .Where(option => !option.Tiles.IsEmpty)
            .ToImmutableArray();
        return new MoveOptions(units, deploys, command, state.Rules.MaxArrivalsPerTurn);
    }

    private static ImmutableArray<ActionOption> ActionOptionsFor(GameState state, int unitId)
    {
        Unit unit = state.Units[unitId];
        List<Unit> enemies = state.FieldUnits.Where(other => state.AreEnemies(other, unit)).ToList();

        IEnumerable<ActionOption> attacks = enemies
            .Select(enemy => (Enemy: enemy, Kind: CombatRules.BasicAttackKind(state, unit, enemy)))
            .Where(pair => pair.Kind is not null)
            .Select(pair => new ActionOption(
                new Attack(unit.Id, pair.Enemy.Id),
                CombatRules.Preview(state, unit, pair.Enemy, pair.Kind ?? AttackKind.Basic)));

        IEnumerable<ActionOption> abilities = state.DefinitionOf(unit).Abilities
            .Where(ability => unit.AbilityReadyTurn.GetValueOrDefault(ability) <= state.Turn)
            .SelectMany(ability => AbilityCandidates(state, unit, ability))
            .Where(command => IsLegal(state, unit.Owner, command))
            .Select(command => new ActionOption(command, AbilityPreview(state, unit, command)));

        IEnumerable<ActionOption> passive = state.TurnState.HasDelayed.Contains(unit.Id)
            ? [new ActionOption(new Wait(unit.Id), null)]
            : [new ActionOption(new Wait(unit.Id), null), new ActionOption(new Delay(unit.Id), null)];

        return [.. attacks, .. abilities, .. passive];
    }

    private static IEnumerable<UseAbility> AbilityCandidates(GameState state, Unit unit, string ability)
    {
        if (AbilityRules.IsUntargeted(ability))
        {
            return [new UseAbility(unit.Id, ability, null)];
        }

        AbilityDefinition definition = state.Rules.Abilities[ability];
        int maxRange = definition.UsesAttackRange ? state.DefinitionOf(unit).MaxRange : definition.MaxRange;
        return state.Map.AllPoints()
            .Where(tile => tile != unit.Position && tile.DistanceTo(unit.Position) <= maxRange)
            .Select(tile => new UseAbility(unit.Id, ability, tile));
    }

    private static AttackPreview? AbilityPreview(GameState state, Unit unit, UseAbility command) =>
        command.Ability == AbilityIds.PinningShot
            && command.Target is Point target
            && state.UnitAt(target) is Unit enemy
            && CombatRules.BasicAttackKind(state, unit, enemy) is AttackKind kind
            ? CombatRules.Preview(state, unit, enemy, kind)
            : null;

    private static bool IsLegal(GameState state, Seat seat, ICommand command) =>
        GameEngine.Apply(state, seat, command) is Accepted;
}
