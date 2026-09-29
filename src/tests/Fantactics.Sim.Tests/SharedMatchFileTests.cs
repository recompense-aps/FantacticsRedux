using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text.Json;
using Fantactics.Ai;
using Fantactics.Core;
using Fantactics.Core.Engine;
using Fantactics.Core.Hosting;
using Fantactics.Core.Players;
using Fantactics.Core.Records;
using Fantactics.Core.Rules;
using Fantactics.Protocol.Connections;
using Fantactics.Protocol.Files;
using Fantactics.Sim.Matches;

namespace Fantactics.Sim.Tests;

/// <summary>
/// The client and the CLI on one match file (TechnicalDesign §2.5): a local human against an LLM seat played through
/// <c>fantactics-sim</c>, with the file as the only link between them.
/// </summary>
public sealed class SharedMatchFileTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("fantactics-shared-tests").FullName;

    private string MatchPath => Path.Combine(_directory, "match.json");

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task ALocalHumanAndAnLlmOnTheCliPlayAWholeMatch()
    {
        using SharedMatchFile file = new(MatchPath, RulesConfig.Default);
        LocalMatch match = new(new MatchHost(RulesConfig.Default, Setup("human", "llm")), CreateBot, file);
        IGameConnection human = match.Connect(Seat.P1);
        ConcurrentQueue<SeatUpdate> updates = [];
        human.Updated += updates.Enqueue;
        RandomAgent player = new(1);
        await match.StartAsync();

        for (int step = 0; step < 3000 && !match.IsOver; step++)
        {
            if (human.Current.Legal is LegalActions legal)
            {
                Assert.Null(await human.SubmitAsync(player.Decide(human.Current.View, legal.Decision, legal)));
                continue;
            }

            updates.Clear();
            Assert.Equal(ExitCodes.Ok, LlmActs());
            await match.RefreshAsync();
            Assert.NotEmpty(updates);
        }

        Assert.True(match.IsOver);
        Assert.False(file.IsDiverged);
        MatchHost onDisk = MatchHost.Resume(RulesConfig.Default, MatchFiles.Read(MatchPath, RulesConfig.Default));
        Assert.Null(onDisk.ResumeWarning);
        Assert.Equal(StateHash.Compute(match.State), StateHash.Compute(onDisk.State));
    }

    [Fact]
    public async Task WatchingPicksUpTheLlmsMoves()
    {
        using SharedMatchFile file = new(MatchPath, RulesConfig.Default);
        LocalMatch match = new(new MatchHost(RulesConfig.Default, Setup("human", "llm")), CreateBot, file);
        IGameConnection human = match.Connect(Seat.P1);
        await match.StartAsync();
        TaskCompletionSource moved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        human.Updated += _ => moved.TrySetResult();
        file.Watch(match);

        Assert.Equal(ExitCodes.Ok, Cli("act", MatchPath, "--as", "P2", "--draft", "Grunt Grunt Tank | Rusher"));

        Assert.Same(moved.Task, await Task.WhenAny(moved.Task, Task.Delay(TimeSpan.FromSeconds(10))));
        Assert.Single(match.ToRecord().Commands);
    }

    [Fact]
    public async Task AReplacedFileStopsSyncing()
    {
        using SharedMatchFile file = new(MatchPath, RulesConfig.Default);
        LocalMatch match = new(new MatchHost(RulesConfig.Default, Setup("human", "llm")), CreateBot, file);
        await match.StartAsync();
        await match.Connect(Seat.P1).SubmitAsync(new Core.Commands.SubmitDraft(["Archer", "Archer", "Scout"], ["Ranger"]));
        string? reason = null;
        file.Diverged += why => reason = why;

        File.Delete(MatchPath);
        Assert.Equal(ExitCodes.Ok, Cli("new", "--out", MatchPath, "--p1", "llm", "--p2", "llm", "--seed", "99"));
        Assert.Equal(ExitCodes.Ok, Cli("act", MatchPath, "--as", "P2", "--draft", "Grunt Grunt Tank | Rusher"));
        await match.RefreshAsync();

        Assert.True(file.IsDiverged);
        Assert.NotNull(reason);
    }

    [Fact]
    public async Task BotsPlayTheSameMovesInTheCliAndTheClient()
    {
        MatchSetup setup = Setup("bot:captain", "bot:warden");
        MatchSession session = MatchSession.Create(RulesConfig.Default, setup);
        session.AdvanceBots();
        LocalMatch match = new(new MatchHost(RulesConfig.Default, setup), CreateBot);
        await match.StartAsync();

        Assert.True(match.IsOver);
        Assert.Equal(StateHash.Compute(session.State), StateHash.Compute(match.State));
    }

    [Fact]
    public async Task TheCliTakesOverASeatTheClientHandsToABot()
    {
        using SharedMatchFile file = new(MatchPath, RulesConfig.Default);
        LocalMatch match = new(new MatchHost(RulesConfig.Default, Setup("human", "llm")), CreateBot, file);
        await match.StartAsync();

        await match.SetControllerAsync(Seat.P1, SeatController.Bot("bumble"));
        Assert.Equal(ExitCodes.Ok, Cli("act", MatchPath, "--as", "P2", "--draft", "Grunt Grunt Tank | Rusher"));

        // The CLI saw the bot label in the file and played P1's placement itself.
        MatchRecord record = MatchFiles.Read(MatchPath, RulesConfig.Default);
        Assert.Equal("bot:bumble", record.Setup.Seats[Seat.P1]);
        Assert.Contains(record.Commands, c => c.Seat == Seat.P1 && c.Command is Core.Commands.PlaceStartingArmy);
    }

    private static MatchSetup Setup(string p1, string p2) => new(
        "riverford",
        5,
        ImmutableSortedDictionary.CreateRange([KeyValuePair.Create(Seat.P1, p1), KeyValuePair.Create(Seat.P2, p2)]));

    private static IPlayerAgent CreateBot(string spec, int seed) => BotFactory.Create(spec, RulesConfig.Default, seed);

    /// <summary>Plays P2's decision through the CLI with simple choices, as an LLM would.</summary>
    private int LlmActs()
    {
        Assert.Equal(ExitCodes.Ok, Cli(out JsonElement legal, "legal", MatchPath, "--as", "P2", "--format", "json"));
        return legal.GetProperty("kind").GetString() switch
        {
            "Draft" => Cli("act", MatchPath, "--as", "P2", "--draft", "Grunt Grunt Tank | Rusher"),
            "Placement" => Cli("act", MatchPath, "--as", "P2", "--orders", string.Join(" ", legal.GetProperty("toPlace")
                .EnumerateArray()
                .Select((unit, row) => $"{unit.GetString()?.Split(' ')[0]}@19,{row}"))),
            "Moves" => Cli("act", MatchPath, "--as", "P2", "--json", """{"$type":"SubmitMoveOrders","moves":[],"deploys":[]}"""),
            _ => Cli("act", MatchPath, "--as", "P2", "--pick", "1"),
        };
    }

    private static int Cli(params string[] args) => Cli(out _, args);

    private static int Cli(out JsonElement result, params string[] args)
    {
        TestConsole console = new();
        int exit = CliHost.Run(args, console);
        string output = console.Output.Trim();
        result = output.StartsWith('{') ? JsonDocument.Parse(output).RootElement.Clone() : default;
        return exit;
    }
}
