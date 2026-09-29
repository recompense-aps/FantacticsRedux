using System.Text.Json;

namespace Fantactics.Sim.Tests;

/// <summary>Drives <c>fantactics-sim</c> in-process the way an LLM seat would (Simulation §6).</summary>
public sealed class CliTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("fantactics-sim-tests").FullName;

    private string MatchPath => Path.Combine(_directory, "match.json");

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void AnLlmSeatCanPlayAWholeMatchAgainstABot()
    {
        Assert.Equal(ExitCodes.Ok, Run(out _, "new", "--out", MatchPath, "--seed", "3", "--format", "json"));

        for (int step = 0; step < 3000; step++)
        {
            Assert.Equal(ExitCodes.Ok, Run(out JsonElement status, "status", MatchPath, "--format", "json"));
            if (status.TryGetProperty("outcome", out _))
            {
                Assert.Equal(ExitCodes.Ok, Run(out JsonElement replay, "replay", MatchPath, "--format", "json"));
                Assert.NotEmpty(replay.GetProperty("events").EnumerateArray());
                return;
            }

            Assert.Equal("P1", OwingSeat(status));
            Assert.Equal(ExitCodes.Ok, Run(out JsonElement legal, "legal", MatchPath, "--as", "P1", "--format", "json"));
            Assert.Equal(ExitCodes.Ok, Act(legal));
        }

        Assert.Fail("The match didn't finish.");
    }

    [Fact]
    public void ActingOutOfTurnExitsWithThree()
    {
        Run(out _, "new", "--out", MatchPath, "--p1", "bot:random", "--p2", "llm");

        Assert.Equal(ExitCodes.NotYourDecision, Run(out JsonElement error, "act", MatchPath, "--as", "P1", "--pick", "1", "--format", "json"));
        Assert.Equal("not-your-decision", error.GetProperty("code").GetString());
    }

    [Fact]
    public void IllegalOrdersExitWithTwoAndLeaveTheMatchUnchanged()
    {
        Run(out _, "new", "--out", MatchPath);
        string before = File.ReadAllText(MatchPath);

        int exit = Run(out JsonElement error, "act", MatchPath, "--as", "P1", "--draft", "Druid Druid", "--format", "json");

        Assert.Equal(ExitCodes.RuleViolation, exit);
        Assert.Equal("duplicate-unique", error.GetProperty("code").GetString());
        Assert.Equal(before, File.ReadAllText(MatchPath));
    }

    [Fact]
    public void BudgetsCanBeSetPerSeat()
    {
        Run(out _, "new", "--out", MatchPath, "--p2", "llm", "--budget", "60", "--p2-budget", "30", "--starting-cap", "45");

        Assert.Equal(ExitCodes.Ok, Run(out JsonElement status, "status", MatchPath, "--format", "json"));
        Assert.Equal(
            ["60/45", "30/45"],
            status.GetProperty("seats").EnumerateArray().Select(seat => seat.GetProperty("budget").GetString()));
        Assert.Equal(ExitCodes.Ok, Run(out JsonElement legal, "legal", MatchPath, "--as", "P2", "--format", "json"));
        Assert.Equal(30, legal.GetProperty("budget").GetInt32());
        Assert.NotEqual(ExitCodes.Ok, Run(out _, "new", "--out", MatchPath, "--force", "--budget", "-1"));
    }

    [Fact]
    public void WaitForReturnsWhenTheSeatOwesAndTimesOutWhenItDoesNot()
    {
        Run(out _, "new", "--out", MatchPath, "--p1", "llm", "--p2", "llm");
        Run(out _, "act", MatchPath, "--as", "P1", "--draft", "Archer Archer Ranger | Scout");

        Assert.Equal(ExitCodes.Ok, Run(out _, "status", MatchPath, "--wait-for", "P2", "--format", "json"));
        Assert.Equal(ExitCodes.NotYourDecision, Run(out _, "status", MatchPath, "--wait-for", "P1", "--timeout", "1", "--format", "json"));
    }

    [Fact]
    public void RuleViolationsNameUnitsByHandle()
    {
        Run(out _, "new", "--out", MatchPath, "--p1", "llm", "--p2", "llm");
        Run(out _, "act", MatchPath, "--as", "P1", "--draft", "Archer Archer Ranger | Scout");
        Run(out _, "act", MatchPath, "--as", "P2", "--draft", "Grunt Grunt Tank | Rusher");
        Run(out _, "act", MatchPath, "--as", "P1", "--orders", "A@0,5 B@0,6 C@0,7");
        Run(out _, "act", MatchPath, "--as", "P2", "--orders", "A@19,5 B@19,6 C@19,7");

        int exit = Run(out JsonElement error, "act", MatchPath, "--as", "P1", "--json", """{"$type":"SubmitMoveOrders","moves":[{"unitId":1,"path":[{"x":1,"y":5},{"x":2,"y":5},{"x":3,"y":5},{"x":4,"y":5},{"x":5,"y":5},{"x":6,"y":5}]}],"deploys":[]}""", "--format", "json");

        Assert.Equal(ExitCodes.RuleViolation, exit);
        Assert.Contains("Unit A's path", error.GetProperty("message").GetString());
    }

    [Fact]
    public void ReplayIsRefusedWhileTheMatchIsRunning()
    {
        Run(out _, "new", "--out", MatchPath);

        Assert.Equal(ExitCodes.NotYourDecision, Run(out _, "replay", MatchPath, "--format", "json"));
    }

    [Fact]
    public void TournamentsReportEveryGame()
    {
        string csv = Path.Combine(_directory, "games.csv");

        Assert.Equal(ExitCodes.Ok, Run(out JsonElement summary, "run", "--games", "4", "--csv", csv, "--format", "json"));

        Assert.Equal(4, summary.GetProperty("games").GetInt32());
        Assert.Equal(5, File.ReadAllLines(csv).Length);
    }

    [Fact]
    public void TournamentsCanPlayARulesVariant()
    {
        string variant = Path.Combine(SourceRoot(), "Fantactics.Sim", "Variants", "pre-engagement.json");

        Assert.Equal(ExitCodes.Ok, Run(out JsonElement baseline, "run", "--games", "1", "--format", "json"));
        Assert.Equal(ExitCodes.Ok, Run(out JsonElement summary, "run", "--games", "1", "--rules", variant, "--format", "json"));

        Assert.NotEqual(baseline.GetProperty("rules").GetString(), summary.GetProperty("rules").GetString());
    }

    [Fact]
    public void ToonOutputIsProduced()
    {
        Run(out _, "new", "--out", MatchPath);
        TestConsole console = new();

        Assert.Equal(ExitCodes.Ok, CliHost.Run(["legal", MatchPath, "--as", "P1", "--format", "toon"], console));
        Assert.Contains("draftable[", console.Output);
    }

    /// <summary>The <c>src</c> folder, found by walking up to the solution file.</summary>
    private static string SourceRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Fantactics.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Can't find Fantactics.sln.");
    }

    private static string OwingSeat(JsonElement status) =>
        status.GetProperty("seats").EnumerateArray()
            .Single(seat => seat.GetProperty("owes").GetString() is { Length: > 0 })
            .GetProperty("seat").GetString() ?? "";

    /// <summary>Plays the first sensible option for whatever P1 owes.</summary>
    private int Act(JsonElement legal)
    {
        string kind = legal.GetProperty("kind").GetString() ?? "";
        return kind switch
        {
            "Draft" => Run(out _, "act", MatchPath, "--as", "P1", "--draft", "Archer Archer Ranger Druid Herbalist | Scout Scout"),
            "Placement" => Run(out _, "act", MatchPath, "--as", "P1", "--orders", Placements(legal)),
            "Moves" => MoveFirstUnit(legal),
            _ => Run(out _, "act", MatchPath, "--as", "P1", "--pick", "1", "--note", "first option"),
        };
    }

    private static string Placements(JsonElement legal) =>
        string.Join(" ", legal.GetProperty("toPlace").EnumerateArray()
            .Select((unit, row) => $"{unit.GetString()?.Split(' ')[0]}@0,{row}"));

    private int MoveFirstUnit(JsonElement legal)
    {
        JsonElement[] reach = [.. legal.GetProperty("reach").EnumerateArray()];
        if (reach.Length == 0)
        {
            return Run(out _, "act", MatchPath, "--as", "P1", "--json", """{"$type":"SubmitMoveOrders","moves":[],"deploys":[]}""");
        }

        JsonElement last = reach[^1];
        string order = $"{last.GetProperty("unit").GetString()}>{last.GetProperty("x").GetInt32()},{last.GetProperty("y").GetInt32()}";
        return Run(out _, "act", MatchPath, "--as", "P1", "--orders", order);
    }

    private static int Run(out JsonElement result, params string[] args)
    {
        TestConsole console = new();
        int exit = CliHost.Run(args, console);
        string output = console.Output.Trim();
        result = output.StartsWith('{') ? JsonDocument.Parse(output).RootElement.Clone() : default;
        return exit;
    }
}
