using System.Text.Json.Nodes;
using Fantactics.Client.Logic.Session;
using Fantactics.Core;
using Fantactics.Core.Engine;
using Fantactics.Core.Hosting;
using Fantactics.Core.Players;
using Fantactics.Core.Records;
using Fantactics.Core.Rules;
using Fantactics.Protocol.Files;

namespace Fantactics.Client.Logic.Tests;

/// <summary>The load screen: which seat it offers first, and what it says about files that can't load.</summary>
public sealed class LoadTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("fantactics-load-tests").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void ACutShortFileIsNotValidJson()
    {
        string path = Write("cut.json", Saved(1)[..200]);

        Assert.Contains("Can't load cut.json: not valid JSON (line", LoadError(path));
        SaveSummary summary = SaveSummary.Read(path);
        Assert.False(summary.Readable);
        Assert.Equal("cut.json: not valid JSON", summary.Text);
    }

    [Fact]
    public void OtherJsonIsNotAMatchFile()
    {
        string path = Write("settings.json", """{"theme":"dark"}""");

        Assert.Equal("Can't load settings.json: not a match file.", LoadError(path));
        Assert.Equal("settings.json: not a match file", SaveSummary.Read(path).Text);
    }

    [Fact]
    public void ANewerFormatIsRefused()
    {
        string path = Write("future.json", Edit(Saved(2), root => root["formatVersion"] = 99));

        Assert.Contains("saved by a newer version of Fantactics", LoadError(path));
        Assert.Equal("future.json: saved by a newer version of Fantactics", SaveSummary.Read(path).Text);
    }

    [Fact]
    public void AMissingCommandListIsReadableNotACrash()
    {
        string path = Write("nocommands.json", Edit(Saved(3), root => root.AsObject().Remove("commands")));

        Assert.Contains("Can't load nocommands.json: The match record has no command list.", LoadError(path));
    }

    [Fact]
    public void AnUnknownMapIsNamed()
    {
        string path = Write("map.json", Edit(Saved(4), root => root["setup"]!["map"] = "atlantis"));

        Assert.Contains("uses the map 'atlantis'", LoadError(path));
    }

    [Fact]
    public void AnOldRulesFileWithoutASnapshotSaysWhichRules()
    {
        string path = Write("old.json", Edit(Saved(5), root =>
        {
            OldRules(root);
            root.AsObject().Remove("snapshot");
        }));

        string error = LoadError(path);

        Assert.Contains("the match can't be continued", error);
        Assert.Contains("0.6.0", error);
        Assert.Contains(GameEngine.RulesVersion, error);
    }

    [Fact]
    public void AnOldRulesFileWithASnapshotLoadsWithAWarning()
    {
        string path = Write("old.json", Edit(Saved(6), OldRules));

        using OpenMatch open = Opener().Load(path);

        Assert.NotNull(open.Session.Match.ResumeWarning);
        Assert.EndsWith(", rules 0.6.0", SaveSummary.Read(path).Text);
    }

    [Fact]
    public void AnUnreadableOldSnapshotMentionsTheRules()
    {
        string path = Write("broken.json", Edit(Saved(7), root =>
        {
            OldRules(root);
            root["snapshot"]!["turn"] = "seven";
        }));

        string error = LoadError(path);

        Assert.Contains("the value at $.snapshot.turn", error);
        Assert.Contains("It was saved with rules 0.6.0", error);
    }

    [Fact]
    public void AMissingFileOrSeatIsSaid()
    {
        string path = Write("done.json", Saved(8));

        Assert.Equal("Can't load gone.json: the file no longer exists.", LoadError(Path.Combine(_folder, "gone.json")));
        Assert.Equal("Can't load done.json: it has no seat P3.", LoadError(path, Seat.P3));
    }

    [Fact]
    public void HotseatOffersTheSeatToMoveAndShowsIt()
    {
        string path = Path.Combine(_folder, "hotseat.json");
        MatchFiles.Write(path, WhereOnlyP2IsToMove(9).ToRecord());
        SaveSummary summary = SaveSummary.Read(path);

        Assert.Equal(
            [new("Play the seat to move", null), new("Play as P1 (human)", Seat.P1), new("Play as P2 (human)", Seat.P2)],
            summary.SeatChoices);
        Assert.Equal(0, summary.DefaultSeatChoice);
        using OpenMatch open = Opener().Load(path, summary.SeatChoices[summary.DefaultSeatChoice].Seat);
        Assert.Equal(Seat.P2, open.Session.Shown);
    }

    [Fact]
    public void AgainstBotsTheHumanSeatComesFirst()
    {
        SaveSummary versusBot = SaveSummary.Read(Write("bot.json", Saved(10, "bot:captain", "human")));
        SaveSummary botsOnly = SaveSummary.Read(Write("bots.json", Saved(11, "bot:captain", "llm")));

        Assert.Equal(
            [new("Play as P1 (bot:captain)", Seat.P1), new("Play as P2 (human)", Seat.P2)],
            versusBot.SeatChoices);
        Assert.Equal(Seat.P2, versusBot.SeatChoices[versusBot.DefaultSeatChoice].Seat);
        Assert.Equal(Seat.P1, botsOnly.SeatChoices[botsOnly.DefaultSeatChoice].Seat);
    }

    /// <summary>The JSON of a short bot match with the given seat labels.</summary>
    private static string Saved(ulong seed, string p1 = "bot:captain", string p2 = "bot:bumble")
    {
        MatchHost host = new(RulesConfig.Default, States.Setup(seed));
        Dictionary<Seat, IPlayerAgent> bots = new()
        {
            [Seat.P1] = States.CreateBot("captain", (int)seed),
            [Seat.P2] = States.CreateBot("bumble", (int)seed + 1),
        };
        for (int i = 0; i < 40 && !host.IsOver; i++)
        {
            Seat seat = GameEngine.PendingDecisions(host.State)[0].Seat;
            host.SubmitEngine(seat, bots[seat].DecideFor(host.State, seat));
        }

        host.SetSeatLabel(Seat.P1, p1);
        host.SetSeatLabel(Seat.P2, p2);
        return host.ToRecord().ToJson();
    }

    /// <summary>A hotseat match played by bots until P2 alone owes a decision.</summary>
    private static MatchHost WhereOnlyP2IsToMove(ulong seed)
    {
        MatchHost host = new(RulesConfig.Default, States.Setup(seed));
        IPlayerAgent bot = States.CreateBot("captain", (int)seed);
        while (GameEngine.PendingDecisions(host.State) is not [{ Seat: Seat.P2 }])
        {
            Assert.False(host.IsOver, "The match ended before P2 alone was to move.");
            Seat seat = GameEngine.PendingDecisions(host.State)[0].Seat;
            host.SubmitEngine(seat, bot.DecideFor(host.State, seat));
        }

        return host;
    }

    /// <summary>Makes the record look older and no longer replay, as after a rules change.</summary>
    private static void OldRules(JsonNode root)
    {
        root["rulesVersion"] = "0.6.0";
        root["commands"]![5]!["stateHashAfter"] = "drifted";
    }

    private static string Edit(string json, Action<JsonNode> edit)
    {
        JsonNode root = JsonNode.Parse(json)!;
        edit(root);
        return root.ToJsonString();
    }

    private string LoadError(string path, Seat? shown = null) =>
        Assert.Throws<MatchLoadException>(() => Opener().Load(path, shown)).Message;

    private string Write(string name, string json)
    {
        string path = Path.Combine(_folder, name);
        File.WriteAllText(path, json);
        return path;
    }

    private MatchOpener Opener() => new(RulesConfig.Default, States.CreateBot, new SaveLocations(_folder));
}
