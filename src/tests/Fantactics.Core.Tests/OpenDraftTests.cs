using System.Collections.Immutable;
using System.Text.Json;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Maps;
using Fantactics.Core.Records;
using Fantactics.Core.Rules;
using Fantactics.Core.Scenarios;
using Fantactics.Core.Serialization;
using Fantactics.Core.State;
using static Fantactics.Core.Tests.Play;

namespace Fantactics.Core.Tests;

/// <summary>
/// The open draft: drafting from every race, allowed-race filters, the draft reveal, own-race scope, and units with
/// more than one race (GameDesign §4.4, RacesAndUnits §2.4).
/// </summary>
public class OpenDraftTests
{
    private static readonly SubmitDraft _mixedDraft = new(["Archer", "Archer", "Tank"], ["Grunt"]);

    [Fact]
    public void AMixedDraftIsAccepted()
    {
        GameState state = Apply(NewMatch(), Seat.P1, _mixedDraft).State;
        state = Apply(state, Seat.P2, new SubmitDraft(["Grunt", "Scout"], [])).State;

        Assert.Equal(Phase.Placement, state.Phase);
        Assert.Equal(
            ["Archer", "Archer", "Tank", "Grunt"],
            state.Units.Values.Where(u => u.Owner == Seat.P1).Select(u => u.Type));
    }

    [Fact]
    public void DraftOptionsListEveryRaceWithRaceAndClasses()
    {
        DraftOptions options = LegalActions.For(NewMatch(), Seat.P1)?.Draft ?? throw new InvalidOperationException();

        Assert.Equal(RulesConfig.Default.Units.Count, options.Units.Length);
        DraftUnitOption archer = Assert.Single(options.Units, unit => unit.Type == "Archer");
        Assert.Equal("Elves", archer.Race);
        Assert.Equal([ClassIds.Ranged], archer.Classes);
        Assert.Empty(Assert.Single(options.Units, unit => unit.Type == "Grunt").Classes);
    }

    [Fact]
    public void AnAllowedRacesFilterLimitsTheDraft()
    {
        GameState state = NewMatch(new Dictionary<Seat, ImmutableSortedSet<string>> { [Seat.P1] = ["Elves"] });

        Rejects(state, Seat.P1, _mixedDraft, "race-not-allowed");
        Assert.All(LegalActions.For(state, Seat.P1)?.Draft?.Units ?? [], unit => Assert.Equal("Elves", unit.Race));
        Assert.Contains(LegalActions.For(state, Seat.P2)?.Draft?.Units ?? [], unit => unit.Race == "Goblins");
    }

    [Fact]
    public void UnknownRacesAreRejectedAtSetup()
    {
        Assert.Throws<ArgumentException>(() =>
            NewMatch(new Dictionary<Seat, ImmutableSortedSet<string>> { [Seat.P1] = ["Dragons"] }));
    }

    [Fact]
    public void RacesAndCountsAreRevealedOnlyOnceBothDraftsAreIn()
    {
        GameState afterP1 = Apply(NewMatch(), Seat.P1, _mixedDraft).State;

        Assert.Null(PlayerView.Project(afterP1, Seat.P2).Players[Seat.P1].DraftedRaces);

        GameState drafted = Apply(afterP1, Seat.P2, new SubmitDraft(["Grunt"], [])).State;
        PlayerView p2View = PlayerView.Project(drafted, Seat.P2);
        ImmutableSortedDictionary<string, int>? enemy = p2View.Players[Seat.P1].DraftedRaces;
        ImmutableSortedDictionary<string, int>? own = p2View.Players[Seat.P2].DraftedRaces;

        Assert.NotNull(enemy);
        Assert.NotNull(own);
        Assert.Equal(new Dictionary<string, int> { ["Elves"] = 2, ["Goblins"] = 2 }, enemy);
        Assert.Equal(new Dictionary<string, int> { ["Goblins"] = 1 }, own);
    }

    [Fact]
    public void MendOnlyHealsUnitsOfTheHerbalistsRace()
    {
        GameState state = HerbalistNextTo("Tank", extraRaces: null, out int herbalist, out int ally);

        Rejects(state, Seat.P1, new UseAbility(herbalist, AbilityIds.Mend, state.Units[ally].Position), "bad-target");
        Assert.DoesNotContain(
            LegalActions.For(state, Seat.P1)?.Actions ?? [],
            option => option.Command is UseAbility { Ability: AbilityIds.Mend });
    }

    [Fact]
    public void MendHealsAHybridThatSharesTheHerbalistsRace()
    {
        GameState state = HerbalistNextTo("Tank", extraRaces: ["Elves"], out int herbalist, out int ally);

        Accepted result = Apply(state, Seat.P1, new UseAbility(herbalist, AbilityIds.Mend, state.Units[ally].Position));

        Assert.Equal(state.Units[ally].Hp + 3, result.State.Units[ally].Hp);
    }

    [Theory]
    [InlineData("Grunt", null, 2)]
    [InlineData("Ranger", null, 0)]
    [InlineData("Ranger", "Goblins", 1)]
    public void WarCryOnlyReachesTheWarLordsRace(string type, string? extraRace, int bloodthirst)
    {
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "WarLord", 1, 1)
            .AddUnit(Seat.P1, type, 2, 1, out int unit)
            .Modify(u => u with { ExtraRaces = extraRace is null ? null : [extraRace] })
            .Build();

        Assert.Equal(bloodthirst, CombatRules.EffectiveBloodthirst(state, state.Units[unit]));
    }

    [Fact]
    public void AHybridHasBothRacesTraits()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Scout", 1, 1, out int scout)
            .Modify(u => u with { ExtraRaces = ["Goblins"] })
            .Build();
        Unit hybrid = state.Units[scout];

        Assert.True(UnitRules.HasTrait(state, hybrid, TraitIds.Forestwalk));
        Assert.True(UnitRules.HasTrait(state, hybrid, TraitIds.MountainBorn));
        Assert.True(UnitRules.HasTrait(state, hybrid, TraitIds.Slippery));
        Assert.Equal(["Elves", "Goblins"], UnitRules.Races(state, hybrid));
    }

    [Fact]
    public void RaceTraitsShareByTheHigherValueAndUnitTraitsWin()
    {
        RaceDefinition undead = new(ImmutableSortedDictionary.CreateRange(
            [KeyValuePair.Create(TraitIds.MountainBorn, 3), KeyValuePair.Create(TraitIds.Bloodthirst, 5)]));
        RulesConfig rules = RulesConfig.Default with { Races = RulesConfig.Default.Races.Add("Undead", undead) };

        ImmutableSortedDictionary<string, int> traits = rules.TraitsOf("Grunt", ["Undead"]);

        Assert.Equal(3, traits[TraitIds.MountainBorn]);
        Assert.Equal(1, traits[TraitIds.Bloodthirst]);
    }

    [Fact]
    public void ALegacySetupLoadsItsRacesAsAFilter()
    {
        const string Json = """
            {"map":"riverford","races":{"P1":"Elves","P2":"Goblins"},"seed":5,"seats":{"P1":"llm","P2":"bot:random"}}
            """;

        MatchSetup setup = JsonSerializer.Deserialize<MatchSetup>(Json, CoreJson.Options)
            ?? throw new InvalidOperationException();

        Assert.Equal(["Elves"], setup.AllowedRaces?[Seat.P1]);
        Assert.Equal(["Goblins"], setup.AllowedRaces?[Seat.P2]);
        Assert.DoesNotContain("\"races\"", JsonSerializer.Serialize(setup, CoreJson.Options));
    }

    private static GameState NewMatch(IReadOnlyDictionary<Seat, ImmutableSortedSet<string>>? allowed = null) =>
        GameEngine.NewMatch(RulesConfig.Default, MapLibrary.Load("riverford"), 42, allowed);

    /// <summary>
    /// The action phase with a Herbalist whose slot is up, next to a wounded friendly <paramref name="type"/>.
    /// </summary>
    private static GameState HerbalistNextTo(
        string type,
        ImmutableSortedSet<string>? extraRaces,
        out int herbalist,
        out int ally)
    {
        GameState state = new ScenarioBuilder()
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Herbalist", 1, 1, out herbalist)
            .AddUnit(Seat.P1, type, 2, 1, out ally)
            .Modify(u => u with { Hp = 3, ExtraRaces = extraRaces })
            .AddUnit(Seat.P2, "Grunt", 6, 4)
            .Build();
        state = Moves(state, SubmitMoveOrders.HoldAll, SubmitMoveOrders.HoldAll).State;
        int first = herbalist;
        return state with
        {
            TurnState = state.TurnState with { ActionQueue = [first, .. state.TurnState.ActionQueue.Remove(first)] },
        };
    }
}
