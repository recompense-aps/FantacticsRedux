using System.Collections.Immutable;
using System.Text.Json;
using Fantactics.Core.Rules;
using Fantactics.Sim.Tournaments;

namespace Fantactics.Sim.Tests;

/// <summary>Tournaments: results depend on seeds only, and summaries stay small.</summary>
public sealed class TournamentTests
{
    [Fact]
    public void ResultsDontDependOnTheThreadCount()
    {
        TournamentRunner runner = new(RulesConfig.Default);
        TournamentOptions options = new(
            "riverford", ["Elves"], ["Goblins"], "captain@easy", "random", Games: 6, Seed: 3, Threads: 1);

        (TournamentSummary oneSummary, ImmutableArray<GameResult> one) = runner.Run(options);
        (TournamentSummary manySummary, ImmutableArray<GameResult> many) = runner.Run(options with { Threads = 4 });

        Assert.Equal(one.AsEnumerable(), many.AsEnumerable());
        Assert.Equal(oneSummary.UnitStats.AsEnumerable(), manySummary.UnitStats.AsEnumerable());
        Assert.Equal(oneSummary.Clashes.AsEnumerable(), manySummary.Clashes.AsEnumerable());
        Assert.Equal(oneSummary.EndReasons.AsEnumerable(), manySummary.EndReasons.AsEnumerable());
        Assert.Equal(oneSummary.Fingerprints.AsEnumerable(), manySummary.Fingerprints.AsEnumerable());
        Assert.Equal(oneSummary.ArmyShapes.AsEnumerable(), manySummary.ArmyShapes.AsEnumerable());
        Assert.Equal(oneSummary.RaceMixes.AsEnumerable(), manySummary.RaceMixes.AsEnumerable());
        Assert.Equal(WithoutTables(oneSummary), WithoutTables(manySummary));
    }

    [Fact]
    public void ClashesAreTalliedPerUnitTypePair()
    {
        TournamentRunner runner = new(RulesConfig.Default);
        TournamentOptions options = new(
            "riverford", null, null, "captain", "captain", Games: 6, Seed: 1, Threads: 0);

        (TournamentSummary summary, _) = runner.Run(options);

        Assert.NotEmpty(summary.Clashes);
        Assert.All(summary.Clashes, clash =>
        {
            Assert.True(string.CompareOrdinal(clash.TypeA, clash.TypeB) <= 0);
            Assert.Equal(clash.Clashes, clash.AWins + clash.BWins + clash.Unresolved);
            Assert.Contains(clash.TypeA, RulesConfig.Default.Units.Keys);
            Assert.Contains(clash.TypeB, RulesConfig.Default.Units.Keys);
        });
    }

    [Fact]
    public void ArmiesAreCountedByShapeAndRaceMix()
    {
        TournamentRunner runner = new(RulesConfig.Default);
        TournamentOptions options = new(
            "riverford", ["Elves"], null, "captain@easy", "random", Games: 4, Seed: 1, Threads: 1);

        (TournamentSummary summary, ImmutableArray<GameResult> games) = runner.Run(options);

        Assert.Equal(8, summary.ArmyShapes.Sum(group => group.Armies));
        Assert.Equal(8, summary.RaceMixes.Sum(group => group.Armies));
        Assert.All(games, game => Assert.Equal("Elves", game.P1Races));
        Assert.Equal(4, Assert.Single(summary.RaceMixes, group => group.Group == "Elves").Armies);
        Assert.All(
            summary.UnitStats.Where(stats => stats.Seat == "P1" && stats.Picked > 0),
            stats => Assert.Equal("Elves", RulesConfig.Default.Units[stats.Type].Race));
    }

    [Fact]
    public void UnknownRacesAreRejected()
    {
        TestConsole console = new();

        int exit = CliHost.Run(["run", "--p1-races", "Elves,Dragons", "--games", "1", "--format", "json"], console);

        Assert.NotEqual(ExitCodes.Ok, exit);
        Assert.Contains("Unknown race 'Dragons'", console.Output);
    }

    [Fact]
    public void TheSummaryStaysSmallHoweverManyGamesArePlayed()
    {
        TestConsole console = new();

        int exit = CliHost.Run(
            ["run", "--p1", "bot:captain", "--p2", "bot:random", "--games", "40", "--threads", "0", "--format", "toon"],
            console);

        Assert.Equal(ExitCodes.Ok, exit);
        Assert.InRange(console.Output.Length, 1, 2_000);
    }

    [Fact]
    public void DetailsGoToFilesAndProfilesCanComeFromAFile()
    {
        string directory = Directory.CreateTempSubdirectory("fantactics-run-tests").FullName;
        try
        {
            string profile = Path.Combine(directory, "custom.json");
            File.WriteAllText(profile, """{ "name": "custom", "difficulty": "easy", "style": { "advance": 2.0 } }""");
            string details = Path.Combine(directory, "out");
            TestConsole console = new();

            int exit = CliHost.Run(
                [
                    "run", "--p1-profile", profile, "--p2", "bot:random", "--games", "3", "--out", details,
                    "--format", "json",
                ],
                console);

            Assert.Equal(ExitCodes.Ok, exit);
            using JsonDocument summary = JsonDocument.Parse(console.Output);
            Assert.Equal(2, summary.RootElement.GetProperty("fingerprints").GetArrayLength());
            Assert.StartsWith("bot:custom(file)", summary.RootElement.GetProperty("p1").GetString());
            Assert.Equal(4, File.ReadAllLines(Path.Combine(details, "games.csv")).Length);
            Assert.Equal(3, File.ReadAllLines(Path.Combine(details, "bots.csv")).Length);
            Assert.True(File.Exists(Path.Combine(details, "units.csv")));
            Assert.True(File.Exists(Path.Combine(details, "clashes.csv")));
            Assert.Equal(JsonValueKind.Array, summary.RootElement.GetProperty("clashes").ValueKind);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("bot:captain", true)]
    [InlineData("bot:captain@novice", true)]
    [InlineData("bot:warden@master", true)]
    [InlineData("bot:bumble", true)]
    [InlineData("bot:random", true)]
    [InlineData("bot:captain@impossible", false)]
    [InlineData("bot:random@hard", false)]
    [InlineData("bot:nobody", false)]
    public void SeatKindsAcceptProfilesAndDifficulties(string value, bool valid)
    {
        if (valid)
        {
            Assert.Equal(value, Matches.SeatKind.Parse(value).Value);
        }
        else
        {
            Assert.Throws<SimException>(() => Matches.SeatKind.Parse(value));
        }
    }

    /// <summary>The summary with its array tables emptied, so the rest compares by value.</summary>
    private static TournamentSummary WithoutTables(TournamentSummary summary) => summary with
    {
        UnitStats = [],
        Clashes = [],
        EndReasons = [],
        Fingerprints = [],
        ArmyShapes = [],
        RaceMixes = [],
    };
}
