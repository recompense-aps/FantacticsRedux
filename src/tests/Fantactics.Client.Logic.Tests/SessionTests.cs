using System.Collections.Immutable;
using Fantactics.Client.Logic.Launch;
using Fantactics.Client.Logic.Session;
using Fantactics.Core;
using Fantactics.Core.Engine;
using Fantactics.Core.Hosting;
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
    }

    [Fact]
    public void RaceListsLimitOnlyTheSeatsGivenOne()
    {
        LaunchArgs args = LaunchArgs.Parse(["--p1-races", "Elves, Goblins", "--p2-races", "any"]);

        Assert.Equal(["Elves", "Goblins"], args.P1Races);
        Assert.Null(args.P2Races);
        Assert.Equal([Seat.P1], args.AllowedRaces()?.Keys ?? []);
        Assert.Null(LaunchArgs.Parse(["--new"]).AllowedRaces());
    }

    [Fact]
    public void BudgetsApplyToBothSeatsUnlessASeatHasItsOwn()
    {
        LaunchArgs args = LaunchArgs.Parse(["--budget", "60", "--p2-budget", "30", "--p1-starting-cap", "45"]);

        ImmutableSortedDictionary<Seat, int>? budgets = args.DraftBudgets();
        ImmutableSortedDictionary<Seat, int>? caps = args.StartingCaps();

        Assert.NotNull(budgets);
        Assert.NotNull(caps);
        Assert.Equal(new Dictionary<Seat, int> { [Seat.P1] = 60, [Seat.P2] = 30 }, budgets);
        Assert.Equal(new Dictionary<Seat, int> { [Seat.P1] = 45 }, caps);
        Assert.Null(LaunchArgs.Parse(["--new"]).DraftBudgets());
        Assert.Throws<ArgumentException>(() => LaunchArgs.Parse(["--budget", "0"]));
        Assert.Throws<ArgumentException>(() => LaunchArgs.Parse(["--budget", "lots"]));
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
        Seat second = first.Opponent();
        await session.SubmitAsync(Core.Commands.SubmitMoveOrders.HoldAll);

        Assert.Equal(second, session.Shown);
        Assert.Contains(second, curtains);
        Assert.IsType<SubmitMoveOrdersDecision>(session.Current.Legal?.Decision);
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
