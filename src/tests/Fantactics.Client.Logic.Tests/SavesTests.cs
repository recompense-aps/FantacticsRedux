using Fantactics.Client.Logic.Debug;
using Fantactics.Client.Logic.Session;
using Fantactics.Core;
using Fantactics.Core.Engine;
using Fantactics.Core.Hosting;
using Fantactics.Core.Records;
using Fantactics.Core.Rules;
using Fantactics.Protocol.Connections;
using Fantactics.Protocol.Files;

namespace Fantactics.Client.Logic.Tests;

/// <summary>Saving, loading, branching, sharing, and the debug panel's text.</summary>
public sealed class SavesTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("fantactics-saves-tests").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public async Task InMemoryMatchesAutosaveEveryTurnAndReloadToTheSameState()
    {
        MatchOpener opener = Opener();
        using OpenMatch open = opener.New(States.Setup(5, "bot:captain", "bot:bumble"), null);
        await open.Session.StartAsync();

        Assert.Null(open.SharedFile);
        Assert.True(open.Session.Match.IsOver);
        Assert.True(File.Exists(opener.Saves.Autosave));

        open.Session.SaveTo(opener.Saves.Quicksave);
        using OpenMatch loaded = opener.Load(opener.Saves.Quicksave);
        Assert.Null(loaded.Session.Match.ResumeWarning);
        Assert.Equal(StateHash.Compute(open.Session.Match.State), StateHash.Compute(loaded.Session.Match.State));
        Assert.Null(loaded.SharedFile);
    }

    [Fact]
    public async Task BranchesContinueFromAnEarlierCommand()
    {
        MatchOpener opener = Opener();
        using OpenMatch open = opener.New(States.Setup(6, "bot:captain", "bot:bumble"), null);
        await open.Session.StartAsync();
        MatchHost halfway = MatchHost.Resume(RulesConfig.Default, open.Session.Match.ToRecord().Truncated(30));

        using OpenMatch branch = opener.Branch(open, 30);

        Assert.True(File.Exists(Path.Combine(_folder, "match.b30.json")));
        Assert.Equal(StateHash.Compute(halfway.State), StateHash.Compute(branch.Session.Match.State));
    }

    [Fact]
    public async Task HandingASeatToAnLlmMovesTheMatchToASharedFile()
    {
        MatchOpener opener = Opener();
        using OpenMatch open = opener.New(States.Setup(7, "human", "bot:captain"), null);
        await open.Session.StartAsync();
        await open.Session.Match.SetControllerAsync(Seat.P2, SeatController.Llm);

        using OpenMatch shared = opener.Share(open);

        Assert.NotNull(shared.SharedFile);
        MatchRecord onDisk = MatchFiles.Read(shared.SharedFile.Path, RulesConfig.Default);
        Assert.Equal("llm", onDisk.Setup.Seats[Seat.P2]);
        Assert.Equal(StateHash.Compute(open.Session.Match.State), StateHash.Compute(shared.Session.Match.State));
    }

    [Fact]
    public async Task TheDebugTextCoversEveryCommand()
    {
        using OpenMatch open = Opener().New(States.Setup(8, "bot:captain", "bot:bumble"), null);
        await open.Session.StartAsync();
        MatchRecord record = open.Session.Match.ToRecord();
        string path = Path.Combine(_folder, "done.json");
        open.Session.SaveTo(path);

        IReadOnlyList<TimelineEntry> timeline = DebugText.Timeline(record, open.Session.Match.Events);

        Assert.Equal(record.Commands.Select(c => c.Seq), timeline.Select(e => e.Seq));
        Assert.Equal(15, timeline[^1].Turn);
        Assert.StartsWith("T1 #", DebugText.Line(open.Session.Match.Events.First(e => e.Turn == 1)));
        Assert.Contains("turn 15", DebugText.Summary(path));
        Assert.Contains("not a match file", DebugText.Summary(Path.Combine(_folder, "missing.json")));
    }

    private MatchOpener Opener() => new(RulesConfig.Default, States.CreateBot, new SaveLocations(_folder));
}
