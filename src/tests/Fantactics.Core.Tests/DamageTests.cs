using Fantactics.Core.Engine;
using Fantactics.Core.Events;
using Fantactics.Core.Rules;
using Fantactics.Core.Scenarios;
using Fantactics.Core.State;

namespace Fantactics.Core.Tests;

/// <summary>The sanity table in RacesAndUnits §6 plus the §4.3 formula details.</summary>
public class DamageTests
{
    private static readonly string[] _map =
    [
        ".....",
        ".%^..",
        ".....",
    ];

    [Theory]
    [InlineData("Archer", Seat.P1, "Grunt", 3, 1, 4)]
    [InlineData("Archer", Seat.P1, "Grunt", 2, 1, 2)]
    [InlineData("Grunt", Seat.P2, "Archer", 3, 1, 2)]
    [InlineData("Grunt", Seat.P2, "Archer", 1, 1, 1)]
    [InlineData("Bruiser", Seat.P2, "Archer", 1, 1, 4)]
    [InlineData("Tank", Seat.P2, "Archer", 3, 1, 1)]
    [InlineData("Archer", Seat.P1, "Tank", 3, 1, 2)]
    public void MatchesTheSanityTable(string attackerType, Seat attackerSeat, string targetType, int tx, int ty, int expected)
    {
        GameState state = new ScenarioBuilder()
            .WithMap(_map)
            .AddUnit(attackerSeat, attackerType, 0, 0, out int attacker)
            .AddUnit(attackerSeat == Seat.P1 ? Seat.P2 : Seat.P1, targetType, tx, ty, out int target)
            .Build();

        int damage = CombatRules.Damage(state, state.Units[attacker], state.Units[target], AttackKind.Basic);

        Assert.Equal(expected, damage);
    }

    [Fact]
    public void SurroundedArcherInForestTakesThreeFromAGrunt()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(_map)
            .AddUnit(Seat.P1, "Archer", 1, 1, out int archer)
            .AddUnit(Seat.P2, "Grunt", 0, 1, out int grunt)
            .AddUnit(Seat.P2, "Grunt", 1, 0)
            .AddUnit(Seat.P2, "Grunt", 1, 2)
            .Build();

        Assert.Equal(3, CombatRules.Damage(state, state.Units[grunt], state.Units[archer], AttackKind.Basic));
    }

    [Fact]
    public void RangedShotsGetNoSupport()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(_map)
            .AddUnit(Seat.P1, "Archer", 0, 0, out int archer)
            .AddUnit(Seat.P1, "Scout", 3, 1)
            .AddUnit(Seat.P1, "Scout", 4, 0)
            .AddUnit(Seat.P2, "Grunt", 3, 0, out int grunt)
            .Build();

        Assert.Equal(4, CombatRules.Damage(state, state.Units[archer], state.Units[grunt], AttackKind.Basic));
    }

    [Fact]
    public void RangedShotsGetSupportWhenTheRuleIsOff()
    {
        GameState state = new ScenarioBuilder(RulesConfig.Default with { SupportMeleeOnly = false })
            .WithMap(_map)
            .AddUnit(Seat.P1, "Archer", 0, 0, out int archer)
            .AddUnit(Seat.P1, "Scout", 3, 1)
            .AddUnit(Seat.P2, "Grunt", 3, 0, out int grunt)
            .Build();

        Assert.Equal(5, CombatRules.Damage(state, state.Units[archer], state.Units[grunt], AttackKind.Basic));
    }

    [Fact]
    public void PointBlankHalvesAttackBeforeAddingSupport()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(_map)
            .AddUnit(Seat.P1, "Ranger", 0, 0, out int ranger)
            .AddUnit(Seat.P1, "Scout", 1, 1)
            .AddUnit(Seat.P2, "Grunt", 1, 0, out int grunt)
            .Build();

        // floor(3 / 2) + 1 Support − 0 = 2.
        Assert.Equal(2, CombatRules.Damage(state, state.Units[ranger], state.Units[grunt], AttackKind.PointBlank));
    }

    [Fact]
    public void DamageIsAtLeastOne()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(_map)
            .AddUnit(Seat.P1, "Herbalist", 0, 2, out int herbalist)
            .AddUnit(Seat.P2, "Tank", 2, 1, out int tank)
            .Build();

        Assert.Equal(1, CombatRules.Damage(state, state.Units[herbalist], state.Units[tank], AttackKind.Basic));
    }

    [Fact]
    public void ArchersClashAtHalfAttack()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(_map)
            .AddUnit(Seat.P1, "Archer", 0, 0, out int archer)
            .AddUnit(Seat.P2, "Grunt", 1, 0, out int grunt)
            .Build();

        Assert.Equal(2, CombatRules.Damage(state, state.Units[archer], state.Units[grunt], AttackKind.Clash));
    }

    [Fact]
    public void RecklessAddsOneInClashesOnly()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(_map)
            .AddUnit(Seat.P2, "Rusher", 0, 0, out int rusher)
            .AddUnit(Seat.P1, "Scout", 0, 2, out int scout)
            .Build();

        Assert.Equal(3, CombatRules.Damage(state, state.Units[rusher], state.Units[scout], AttackKind.Basic));
        Assert.Equal(4, CombatRules.Damage(state, state.Units[rusher], state.Units[scout], AttackKind.Clash));
    }
}
