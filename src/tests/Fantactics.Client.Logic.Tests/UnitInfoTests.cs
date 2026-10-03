using System.Collections.Immutable;
using System.Reflection;
using Fantactics.Client.Logic.Board;
using Fantactics.Client.Logic.Input;
using Fantactics.Core;
using Fantactics.Core.Engine;
using Fantactics.Core.Geometry;
using Fantactics.Core.Hosting;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Client.Logic.Tests;

/// <summary>The unit info panel's model: what it says about a unit, and which unit it shows.</summary>
public class UnitInfoTests
{
    private static readonly RulesConfig _rules = RulesConfig.Default;

    [Fact]
    public void EveryAbilityTraitAndStatusHasADescription()
    {
        Assert.All(Constants(typeof(AbilityIds)), id => Assert.NotEmpty(RulesText.Ability(id, _rules.Abilities[id])));
        Assert.All(Constants(typeof(TraitIds)), id => Assert.NotEmpty(RulesText.Trait(id, 1)));
        Assert.All(Enum.GetValues<StatusKind>(), status => Assert.NotEmpty(RulesText.Status(status)));
    }

    [Fact]
    public void AnEnemyUnitShowsItsTypeRaceHpAndStats()
    {
        SeatUpdate update = Movement().First(u => u.View.Units.Any(unit => unit.IsOnField && unit.Owner != u.View.Seat));
        Unit enemy = update.View.Units.First(unit => unit.IsOnField && unit.Owner != update.View.Seat);
        UnitDefinition type = _rules.Units[enemy.Type];

        UnitInfo info = UnitInfo.Of(update.View, _rules, enemy);

        Assert.Equal(UnitText.Words(enemy.Type), info.Name);
        Assert.StartsWith($"{enemy.Owner} · enemy · {type.Race}", info.Tags);
        Assert.Equal((enemy.Hp, type.Hp), (info.Hp, info.MaxHp));
        Assert.Equal(UnitText.CombatStats(type), info.Stats);
    }

    [Fact]
    public void StatusesShowTheirLastTurnAndAbilitiesWhenTheyAreReady()
    {
        Unit ranger = Unit.Create(1, Seat.P1, "Ranger", UnitLocation.Field, new Point(0, 0), 5) with
        {
            Statuses = ImmutableSortedDictionary.CreateRange([KeyValuePair.Create(StatusKind.Rooted, 4)]),
            AbilityReadyTurn = ImmutableSortedDictionary.CreateRange([KeyValuePair.Create(AbilityIds.PinningShot, 6)]),
        };

        UnitInfo onCooldown = UnitInfo.Of(ranger, _rules, 3, TurnState.Empty, "yours");
        UnitInfo later = UnitInfo.Of(ranger, _rules, 6, TurnState.Empty, "yours");

        InfoLine rooted = Assert.Single(onCooldown.Statuses);
        Assert.Equal("Rooted · until end of turn 4", rooted.Title);
        Assert.Contains("Can't move", rooted.Description);
        Assert.Equal(
            "Pinning Shot · attack range, line of sight · Cooldown 2 · ready turn 6",
            Assert.Single(onCooldown.Abilities).Title);
        Assert.EndsWith("· ready", Assert.Single(later.Abilities).Title);
        Assert.Contains(onCooldown.Traits, trait => trait.Title == "Forestwalk");
    }

    [Fact]
    public void TraitsMergeRaceTraitsAndShowValuesThatMatter()
    {
        Unit tank = Unit.Create(1, Seat.P2, "Tank", UnitLocation.Reserve, new Point(0, 0), 8);

        UnitInfo info = UnitInfo.Of(tank, _rules, 1, TurnState.Empty, "enemy");

        Assert.Equal(
            ["Bloodthirst 3", "Braced", "Mountain Born", "Out Of The Caves", "Retaliate"],
            info.Traits.Select(trait => trait.Title));
        Assert.Contains("Heals 3 HP", info.Traits[0].Description);
        Assert.Equal("P2 · enemy · Goblins · Defender", info.Tags);
        Assert.Equal("In reserve (Cost 4)", info.Notes);
        Assert.Empty(info.Abilities);
    }

    [Fact]
    public void ThePanelShowsTheHoveredUnitThenTheSelectedOne()
    {
        SeatUpdate update = Movement().First(u =>
            u.Legal?.Moves is { } moves
            && moves.Units.Count(m => m.Destinations.Length > 0) >= 2);
        MoveOrderBuilder builder = new(update.View, update.Legal!.Moves!);
        ImmutableArray<UnitMoveOptions> movers = [.. update.Legal.Moves!.Units.Where(m => m.Destinations.Length > 0)];
        Unit other = update.View.Units.First(u => u.Id == movers[1].UnitId);
        Point empty = update.View.Map.AllPoints().First(tile => update.View.Units.All(u => !u.IsOnField || u.Position != tile));

        Assert.Null(BoardModel.Build(update.View, _rules, builder, null, empty).Info);

        builder.Select(movers[0].UnitId);
        Assert.Equal(movers[0].UnitId, BoardModel.Build(update.View, _rules, builder, null, empty).Info?.UnitId);
        Assert.Equal(other.Id, BoardModel.Build(update.View, _rules, builder, null, other.Position).Info?.UnitId);
    }

    [Fact]
    public void WithNothingHoveredThePanelShowsTheActingUnit()
    {
        SeatUpdate update = new ulong[] { 1, 2, 3 }
            .SelectMany(States.Along)
            .Where(s => s.State.Phase == Phase.Action)
            .Select(s => new SeatUpdate(PlayerView.Project(s.State, s.Seat), [], LegalActions.ForView(s.State, s.Seat)))
            .First(u => u.Legal?.Decision is ChooseUnitActionDecision);
        int acting = ((ChooseUnitActionDecision)update.Legal!.Decision).UnitId;

        Assert.Equal(acting, BoardModel.Build(update.View, _rules, null, null, null).Info?.UnitId);
    }

    private static IEnumerable<string> Constants(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!);

    private static IEnumerable<SeatUpdate> Movement() => new ulong[] { 1, 2, 3 }
        .SelectMany(States.Along)
        .Where(s => s.State.Phase == Phase.Movement)
        .Select(s => new SeatUpdate(PlayerView.Project(s.State, s.Seat), [], LegalActions.ForView(s.State, s.Seat)));
}
