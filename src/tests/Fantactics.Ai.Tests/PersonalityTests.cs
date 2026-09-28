using Fantactics.Ai.Planning;
using Fantactics.Ai.Profiles;
using Fantactics.Core;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Ai.Tests;

/// <summary>The personality roster: each plays its own way, and each clears its bar.</summary>
public class PersonalityTests
{
    [Theory]
    [InlineData("captain")]
    [InlineData("warden")]
    [InlineData("berserker")]
    [InlineData("trickster")]
    public void PersonalitiesBeatRandomWithEitherRaceAndSeat(string bot)
    {
        int wins = WinsAgainst(bot, "random", games: 12);

        Assert.True(wins >= 10, $"{bot} won only {wins} of 12 against random.");
    }

    [Fact]
    public void BumbleLosesToAnEasyCaptain()
    {
        int wins = WinsAgainst("bumble", "captain@easy", games: 12);

        Assert.True(wins <= 1, $"Bumble won {wins} of 12 against captain@easy.");
    }

    [Fact]
    public void ProfilesChooseTheirStances()
    {
        IEnumerable<Stance> berserker = StancesOf(BotLibrary.Profile("berserker").Style);
        IEnumerable<Stance> bumble = StancesOf(BotLibrary.Profile("bumble").Style);
        IEnumerable<Stance> unset = StancesOf(new StyleWeights());

        Assert.Contains(Stance.AllIn, berserker);
        Assert.DoesNotContain(Stance.FallBack, berserker);
        Assert.Equal([Stance.AllIn], bumble);
        Assert.Equal(StanceTemplates.All, unset);
    }

    [Fact]
    public void ALineFormationSpreadsWiderThanABlock()
    {
        Assert.True(PlacementHeight("warden") > PlacementHeight("captain"));
        Assert.Equal(Formation.Line, BotLibrary.Profile("warden").Style.Formation);
    }

    [Fact]
    public void PatientBotsKeepABiggerReserve()
    {
        Assert.True(ReserveCost("warden") > ReserveCost("berserker"));
    }

    private static IEnumerable<Stance> StancesOf(StyleWeights style) =>
        StanceTemplates.For(style).Select(stance => stance.Stance);

    private static int WinsAgainst(string bot, string opponent, int games) =>
        Enumerable.Range(1, games)
            .AsParallel()
            .Count(seed =>
            {
                bool botIsP1 = seed % 2 == 0;
                (string p1Race, string p2Race) = seed % 4 < 2 ? ("Elves", "Goblins") : ("Goblins", "Elves");
                MatchResult result = MatchRunner.Run(
                    RulesConfig.Default,
                    TestMatches.Riverford((ulong)seed, p1Race, p2Race),
                    botIsP1 ? TestMatches.Bots(bot, opponent, seed) : TestMatches.Bots(opponent, bot, seed),
                    keepRecord: false);
                return result.Outcome.Winner == (botIsP1 ? Seat.P1 : Seat.P2);
            });

    /// <summary>Rows spanned by the bot's starting placement as P1, after a fixed draft.</summary>
    private static int PlacementHeight(string bot)
    {
        GameState state = TestMatches.Riverford(1).CreateInitialState(RulesConfig.Default);
        SubmitDraft draft = new(["Archer", "Archer", "Ranger", "Scout", "Scout", "Herbalist", "Scout"], []);
        state = Accept(state, Seat.P1, draft);
        state = Accept(state, Seat.P2, new SubmitDraft(["Grunt"], []));
        PlaceStartingArmy placement = (PlaceStartingArmy)Decide(bot, state, Seat.P1);
        return placement.Placements.Max(p => p.Tile.Y) - placement.Placements.Min(p => p.Tile.Y) + 1;
    }

    private static int ReserveCost(string bot)
    {
        GameState state = TestMatches.Riverford(1).CreateInitialState(RulesConfig.Default);
        SubmitDraft draft = (SubmitDraft)Decide(bot, state, Seat.P1);
        return draft.Reserve.Sum(type => RulesConfig.Default.Units[type].Cost);
    }

    private static ICommand Decide(string bot, GameState state, Seat seat)
    {
        TacticalAgent agent = new(BotLibrary.Profile(bot), RulesConfig.Default, seed: 1);
        LegalActions legal = LegalActions.For(state, seat) ?? throw new InvalidOperationException("Nothing to decide.");
        return agent.Decide(PlayerView.Project(state, seat), legal.Decision, legal);
    }

    private static GameState Accept(GameState state, Seat seat, ICommand command) =>
        GameEngine.Apply(state, seat, command) is Accepted accepted
            ? accepted.State
            : throw new InvalidOperationException($"{command} was rejected.");
}
