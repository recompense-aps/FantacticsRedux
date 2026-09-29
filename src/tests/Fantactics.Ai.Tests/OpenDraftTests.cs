using System.Collections.Immutable;
using Fantactics.Ai.Belief;
using Fantactics.Ai.Profiles;
using Fantactics.Core;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Players;
using Fantactics.Core.Rules;
using Fantactics.Core.Scenarios;
using Fantactics.Core.State;

namespace Fantactics.Ai.Tests;

/// <summary>Bots in the open draft (GameDesign §4.4): race focus when drafting, and the draft reveal in beliefs.</summary>
public class OpenDraftTests
{
    [Fact]
    public void AStrongRaceFocusDraftsOneRace()
    {
        SubmitDraft draft = Draft(raceFocus: 50);

        Assert.Single(Races(draft));
    }

    [Fact]
    public void WithoutRaceFocusTheCaptainMixesRaces()
    {
        SubmitDraft draft = Draft(raceFocus: 0);

        Assert.True(Races(draft).Count() > 1, $"Drafted {string.Join(" ", draft.Starting.Concat(draft.Reserve))}.");
    }

    [Fact]
    public void TheReserveGuessUsesTheRevealedRaces()
    {
        // P2 drafted only Goblins, so a 6-point reserve can't be an elf Ranger even though every race was allowed.
        GameState state = new ScenarioBuilder()
            .AddUnit(Seat.P1, "Archer", 1, 5)
            .AddUnit(Seat.P2, "Grunt", 18, 5)
            .AddReserve(Seat.P2, "Tank", out _)
            .AddReserve(Seat.P2, "Grunt", out _)
            .Build();

        ImmutableArray<string> guess = ReserveGuesser.Guess(PlayerView.Project(state, Seat.P1), RulesConfig.Default);

        Assert.Equal(6, guess.Sum(type => RulesConfig.Default.Units[type].Cost));
        Assert.All(guess, type => Assert.Equal("Goblins", RulesConfig.Default.Units[type].Race));
    }

    private static SubmitDraft Draft(double raceFocus)
    {
        BotProfile captain = BotLibrary.Profile("captain");
        BotProfile profile = captain with { Style = captain.Style with { RaceFocus = raceFocus } };
        GameState state = TestMatches.Open(1).CreateInitialState(RulesConfig.Default);
        return (SubmitDraft)new TacticalAgent(profile, RulesConfig.Default, seed: 1).DecideFor(state, Seat.P1);
    }

    private static IEnumerable<string> Races(SubmitDraft draft) =>
        draft.Starting
            .Concat(draft.Reserve)
            .Select(type => RulesConfig.Default.Units[type].Race)
            .Distinct();
}
