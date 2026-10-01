using System.Collections.Immutable;
using Fantactics.Client.Logic.Board;
using Fantactics.Client.Logic.Launch;
using Fantactics.Client.Logic.Session;
using Fantactics.Core;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Hosting;
using Fantactics.Core.Players;
using Fantactics.Core.Records;
using Fantactics.Core.Rules;
using Fantactics.Protocol.Connections;

namespace Fantactics.Client.Logic.Tests;

/// <summary>Launch options and the screen's view of a match.</summary>
public class SessionTests
{
    [Fact]
    public void LaunchArgsParse()
    {
        LaunchArgs args = LaunchArgs.Parse(["--p2", "bot:warden@hard", "--seed", "7", "--draft-as", "none", "--speed", "2"]);

        Assert.True(args.NewMatch);
        Assert.Equal("human", args.P1);
        Assert.Equal("bot:warden@hard", args.P2);
        Assert.Equal(7UL, args.Seed);
        Assert.Null(args.DraftAs);
        Assert.Equal(2, args.Speed);
        Assert.False(LaunchArgs.Parse([]).SkipsMenu);
        Assert.Equal(Seat.P2, LaunchArgs.Parse(["--load", "x.json", "--as", "p2"]).As);
        Assert.StartsWith("bot:", LaunchArgs.Parse(["--autoplay"]).P1);
        Assert.Throws<ArgumentException>(() => LaunchArgs.Parse(["--load"]));
        Assert.Throws<ArgumentException>(() => LaunchArgs.Parse(["--bogus"]));
        Assert.Equal("load", LaunchArgs.Parse(["--menu", "load"]).Menu);
        Assert.Throws<ArgumentException>(() => LaunchArgs.Parse(["--menu", "shop"]));
    }

    [Fact]
    public void RaceListsLimitOnlyTheSeatsGivenOne()
    {
        LaunchArgs args = LaunchArgs.Parse(["--p1-races", "Elves, Goblins", "--p2-races", "any"]);

        Assert.Equal(["Elves", "Goblins"], args.P1Races);
        Assert.Null(args.P2Races);
        Assert.Equal([Seat.P1], args.Form(RulesConfig.Default).ToSetup(RulesConfig.Default, 1).AllowedRaces?.Keys ?? []);
        Assert.Null(LaunchArgs.Parse(["--new"]).Form(RulesConfig.Default).ToSetup(RulesConfig.Default, 1).AllowedRaces);
    }

    [Fact]
    public void BudgetsApplyToBothSeatsUnlessASeatHasItsOwn()
    {
        LaunchArgs args = LaunchArgs.Parse(["--budget", "60", "--p2-budget", "30", "--p1-starting-cap", "45"]);

        RulesConfig rules = RulesConfig.Default with { DraftBudget = 40, StartingCap = 30 };
        MatchSetup setup = args.Form(rules).ToSetup(rules, 1);
        ImmutableSortedDictionary<Seat, int>? budgets = setup.DraftBudgets;
        ImmutableSortedDictionary<Seat, int>? caps = setup.StartingCaps;

        Assert.NotNull(budgets);
        Assert.NotNull(caps);
        Assert.Equal(new Dictionary<Seat, int> { [Seat.P1] = 60, [Seat.P2] = 30 }, budgets);
        Assert.Equal(new Dictionary<Seat, int> { [Seat.P1] = 45 }, caps);
        Assert.Null(LaunchArgs.Parse(["--new"]).Form(rules).ToSetup(rules, 1).DraftBudgets);
        Assert.Null(LaunchArgs.Parse(["--budget", "40"]).Form(rules).ToSetup(rules, 1).DraftBudgets);
        Assert.Throws<ArgumentException>(() => LaunchArgs.Parse(["--budget", "0"]));
        Assert.Throws<ArgumentException>(() => LaunchArgs.Parse(["--budget", "lots"]));
    }

    [Fact]
    public void ThirdAndFourthSeatsAndTeamsComeFromTheLaunchOptions()
    {
        RulesConfig rules = RulesConfig.Default;
        LaunchArgs args = LaunchArgs.Parse(
        [
            "--map", "crossroads", "--p3", "bot:random", "--p4", "llm", "--p3-races", "Goblins", "--teams", "1,2,1,2",
        ]);

        MatchSetup setup = args.Form(rules).ToSetup(rules, 1);

        Assert.True(args.NewMatch);
        Assert.Equal([Seat.P1, Seat.P2, Seat.P3, Seat.P4], setup.Seats.Keys);
        Assert.Equal("llm", setup.Seats[Seat.P4]);
        Assert.Equal(
            new Dictionary<Seat, int> { [Seat.P1] = 1, [Seat.P2] = 2, [Seat.P3] = 1, [Seat.P4] = 2 },
            setup.Teams!);
        Assert.Equal(["Goblins"], setup.AllowedRaces![Seat.P3]);
        Assert.Empty(args.Form(rules).Problems(rules, _ => true));
        Assert.NotEmpty(LaunchArgs.Parse(["--p3", "llm"]).Form(rules).Problems(rules, _ => true));
        Assert.Throws<ArgumentException>(() => LaunchArgs.Parse(["--p4", "llm"]).Form(rules));
        Assert.Throws<ArgumentException>(() => LaunchArgs.Parse(["--teams", "1,2,1"]).Form(rules));
        Assert.Throws<ArgumentException>(() => LaunchArgs.Parse(["--teams", "1,x"]));
    }

    [Fact]
    public async Task AFourSeatSessionShowsEveryPlayer()
    {
        MatchSetup setup = new(
            "crossroads",
            6,
            new Dictionary<Seat, string>
            {
                [Seat.P1] = "human",
                [Seat.P2] = "bot:captain@easy",
                [Seat.P3] = "bot:captain@easy",
                [Seat.P4] = "bot:captain@easy",
            }.ToImmutableSortedDictionary());
        LocalMatch match = new(new MatchHost(RulesConfig.Default, setup), States.CreateBot);
        ClientSession session = new(match, RulesConfig.Default, States.CreateBot, "captain");
        TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Updated += update =>
        {
            if (update.Legal?.Decision is SubmitMoveOrdersDecision)
            {
                ready.TrySetResult();
            }
        };

        await session.StartAsync();

        Assert.Same(ready.Task, await Task.WhenAny(ready.Task, Task.Delay(TimeSpan.FromSeconds(10))));
        PlayerView view = session.Current.View;
        Assert.Equal(
            ["P1 · you", "P2 · enemy", "P3 · enemy", "P4 · enemy"],
            HudText.Players(view).Select(line => string.Join(" · ", line.Text.Split(" · ").Take(2))));
        Assert.DoesNotContain("–", HudText.Status(view, RulesConfig.Default));
    }

    [Fact]
    public void SavesGoToTheRepoPlaytestsFolderInDevelopment()
    {
        string project = Path.Combine(RepoRoot(), "src", "Fantactics.Client");

        Assert.Equal(Path.Combine(RepoRoot(), "playtests"), SaveLocations.Choose(null, project, "user", exported: false).Folder);
        Assert.Equal("user", SaveLocations.Choose(null, project, "user", exported: true).Folder);
        Assert.Equal(Path.GetFullPath("elsewhere"), SaveLocations.Choose("elsewhere", project, "user", exported: false).Folder);
        Assert.Equal(Path.Combine("dir", "m.b12.json"), SaveLocations.BranchFile(Path.Combine("dir", "m.json"), 12));
    }

    [Fact]
    public async Task TheQuickStartDraftsAndPlacesForTheHuman()
    {
        LocalMatch match = new(new MatchHost(RulesConfig.Default, States.Setup(3, "human", "bot:captain")), States.CreateBot);
        ClientSession session = new(match, RulesConfig.Default, States.CreateBot, "captain");
        TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Updated += update =>
        {
            if (update.Legal?.Decision is SubmitMoveOrdersDecision)
            {
                ready.TrySetResult();
            }
        };

        await session.StartAsync();

        Assert.Same(ready.Task, await Task.WhenAny(ready.Task, Task.Delay(TimeSpan.FromSeconds(10))));
        Assert.Equal(Seat.P1, session.Shown);
        Assert.Equal(1, session.Current.View.Turn);
    }

    [Fact]
    public async Task InHotseatTheShownSeatFollowsWhoeverOwesADecision()
    {
        LocalMatch match = new(new MatchHost(RulesConfig.Default, States.Setup(4)), States.CreateBot);
        ClientSession session = new(match, RulesConfig.Default, States.CreateBot, "captain");
        List<Seat> curtains = [];
        session.ShownChanged += curtains.Add;
        await session.StartAsync();

        for (int step = 0; step < 50 && session.Current.Legal?.Decision is not SubmitMoveOrdersDecision; step++)
        {
            await Task.Delay(20);
        }

        Seat first = session.Shown;
        Seat second = first == Seat.P1 ? Seat.P2 : Seat.P1;
        await session.SubmitAsync(Core.Commands.SubmitMoveOrders.HoldAll);

        Assert.Equal(second, session.Shown);
        Assert.Contains(second, curtains);
        Assert.IsType<SubmitMoveOrdersDecision>(session.Current.Legal?.Decision);
    }

    [Fact]
    public async Task LoadingHotseatAsTheIdleSeatPassesToTheSeatThatOwesADecision()
    {
        MatchHost host = new(RulesConfig.Default, States.Setup(5));
        host.SubmitEngine(Seat.P1, States.CreateBot("captain", 1).DecideFor(host.State, Seat.P1));
        LocalMatch match = new(host, States.CreateBot);
        ClientSession session = new(match, RulesConfig.Default, States.CreateBot, draftAs: null, shown: Seat.P1);
        List<Seat> curtains = [];
        session.ShownChanged += curtains.Add;

        await session.StartAsync();

        Assert.Equal([Seat.P2], curtains);
        Assert.Equal(Seat.P2, session.Shown);
        Assert.IsType<DraftArmyDecision>(session.Current.Legal?.Decision);
        Assert.Equal(Seat.P2, new ClientSession(match, RulesConfig.Default, States.CreateBot, null).Shown);
    }

    [Fact]
    public async Task AHotseatMatchFromDraftToEndOnlyEverShowsTheSeatBehindTheLastCurtain()
    {
        LocalMatch match = new(new MatchHost(RulesConfig.Default, States.Setup(6)), States.CreateBot);
        ClientSession session = new(match, RulesConfig.Default, States.CreateBot, draftAs: null);
        List<(Seat? Curtain, Seat? Shown)> seen = [];
        session.ShownChanged += seat =>
        {
            lock (seen)
            {
                seen.Add((seat, null));
            }
        };
        session.Updated += update =>
        {
            lock (seen)
            {
                seen.Add((null, update.View.Seat));
            }
        };
        await session.StartAsync();

        int decisions = 0;
        while (session.Current.View.Outcome is null && decisions < 5000)
        {
            SeatUpdate update = session.Current;
            if (update.Legal is not LegalActions legal)
            {
                await Task.Delay(5);
                continue;
            }

            ICommand command = update.Legal.Decision is DraftArmyDecision or PlaceStartingArmyDecision
                ? session.Suggest("captain", decisions) ?? throw new InvalidOperationException("No suggestion.")
                : States.CreateBot("captain", decisions).Decide(update.View, legal.Decision, legal);
            Assert.Null(await session.SubmitAsync(command));
            decisions++;
        }

        Assert.NotNull(session.Current.View.Outcome);
        Seat behindCurtain = Seat.P1;
        lock (seen)
        {
            Assert.Contains(seen, item => item.Curtain == Seat.P2);
            Assert.Contains(seen, item => item.Curtain == Seat.P1);
            foreach ((Seat? curtain, Seat? shown) in seen)
            {
                behindCurtain = curtain ?? behindCurtain;
                Assert.True(shown is null || shown == behindCurtain, $"{shown}'s view was shown behind {behindCurtain}'s curtain.");
            }
        }
    }

    /// <summary>The repository root, found by walking up to the <c>.git</c> folder.</summary>
    private static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Can't find the repository root.");
    }
}
