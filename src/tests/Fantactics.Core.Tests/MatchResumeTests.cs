using System.Collections.Immutable;
using System.Text.Json;
using Fantactics.Core.Engine;
using Fantactics.Core.Hosting;
using Fantactics.Core.Records;
using Fantactics.Core.Rules;
using Fantactics.Core.Serialization;
using Fantactics.Core.State;

namespace Fantactics.Core.Tests;

/// <summary>Save files: records with snapshots, and where a loaded match continues from.</summary>
public class MatchResumeTests
{
    [Fact]
    public void ASavedMatchResumesWithItsHistory()
    {
        MatchHost original = Autoplay.Run(RulesConfig.Default, seed: 3, decisions: 60);

        MatchRecord saved = MatchRecord.FromJson(original.ToRecord().ToJson(), RulesConfig.Default);
        MatchHost resumed = MatchHost.Resume(RulesConfig.Default, saved);

        Assert.Null(resumed.ResumeWarning);
        Assert.Equal(StateHash.Compute(original.State), StateHash.Compute(resumed.State));
        Assert.Equal(Json(original.Commands), Json(resumed.Commands));
        Assert.Equal(Json(original.Events), Json(resumed.Events));
    }

    [Fact]
    public void FormatOneRecordsStillLoad()
    {
        MatchHost original = Autoplay.Run(RulesConfig.Default, seed: 4, decisions: 30);
        MatchRecord v1 = original.ToRecord() with { FormatVersion = 1, Snapshot = null };

        MatchHost resumed = MatchHost.Resume(RulesConfig.Default, MatchRecord.FromJson(v1.ToJson(), RulesConfig.Default));

        Assert.Null(resumed.ResumeWarning);
        Assert.Equal(StateHash.Compute(original.State), StateHash.Compute(resumed.State));
    }

    [Fact]
    public void AnEditedSnapshotBecomesTheNewStart()
    {
        MatchHost original = Autoplay.Run(RulesConfig.Default, seed: 5, decisions: 40);
        Unit unit = original.State.FieldUnits.First();
        GameState edited = original.State.WithUnit(unit with { Hp = 1 });
        MatchRecord saved = original.ToRecord() with { Snapshot = edited };

        MatchHost resumed = MatchHost.Resume(RulesConfig.Default, MatchRecord.FromJson(saved.ToJson(), RulesConfig.Default));

        Assert.Contains("edited", resumed.ResumeWarning);
        Assert.Empty(resumed.Commands);
        Assert.Equal(1, resumed.State.Units[unit.Id].Hp);

        // Play goes on from the edited position, and the next save replays from it.
        Autoplay.Continue(resumed, seed: 5, decisions: 10);
        MatchHost again = MatchHost.Resume(RulesConfig.Default, MatchRecord.FromJson(resumed.ToRecord().ToJson(), RulesConfig.Default));
        Assert.Null(again.ResumeWarning);
        Assert.Equal(StateHash.Compute(resumed.State), StateHash.Compute(again.State));
    }

    [Fact]
    public void AHistoryThatNoLongerReplaysFallsBackToTheSnapshot()
    {
        MatchHost original = Autoplay.Run(RulesConfig.Default, seed: 6, decisions: 40);
        MatchRecord drifted = Drift(original.ToRecord());

        MatchHost resumed = MatchHost.Resume(RulesConfig.Default, drifted);

        Assert.Contains("drift", resumed.ResumeWarning);
        Assert.Empty(resumed.Commands);
        Assert.Equal(StateHash.Compute(original.State), StateHash.Compute(resumed.State));
    }

    [Fact]
    public void AHistoryThatNoLongerReplaysWithoutASnapshotCannotResume()
    {
        MatchRecord drifted = Drift(Autoplay.Run(RulesConfig.Default, seed: 6, decisions: 40).ToRecord()) with { Snapshot = null };

        Assert.Throws<MatchResumeException>(() => MatchHost.Resume(RulesConfig.Default, drifted));
    }

    [Fact]
    public void TruncatedRecordsBranchFromEarlierPoints()
    {
        MatchHost original = Autoplay.Run(RulesConfig.Default, seed: 7, decisions: 50);
        MatchHost halfway = Autoplay.Run(RulesConfig.Default, seed: 7, decisions: 25);

        MatchHost branch = MatchHost.Resume(RulesConfig.Default, original.ToRecord().Truncated(halfway.Commands.Count));

        Assert.Null(branch.ResumeWarning);
        Assert.Equal(StateHash.Compute(halfway.State), StateHash.Compute(branch.State));
    }

    [Fact]
    public void CatchingUpOnAnotherRecordPublishesTheSameUpdates()
    {
        MatchHost source = Autoplay.Run(RulesConfig.Default, seed: 8, decisions: 60);
        MatchHost follower = MatchHost.Resume(RulesConfig.Default, source.ToRecord().Truncated(20));
        List<(Seat Seat, SeatUpdate Update)> updates = [];
        follower.Updated += (seat, update) => updates.Add((seat, update));

        Assert.True(follower.CatchUp(source.Commands.Skip(20)));

        Assert.Equal(StateHash.Compute(source.State), StateHash.Compute(follower.State));
        Assert.Equal(Json(source.Events), Json(follower.Events));
        Assert.Equal(
            source.Events.Skip(follower.Events.Count(e => e.Seq <= 20)).Count(),
            updates.Where(u => u.Seat == Seat.P1).Sum(u => u.Update.Events.Length));
    }

    [Fact]
    public void CatchingUpStopsWhereTheRecordsDiverge()
    {
        MatchHost source = Autoplay.Run(RulesConfig.Default, seed: 9, decisions: 40);
        MatchHost follower = MatchHost.Resume(RulesConfig.Default, source.ToRecord().Truncated(10));
        ImmutableArray<RecordedCommand> tampered = [.. source.Commands.Skip(10).Select((c, i) => i == 5 ? c with { StateHashAfter = "x" } : c)];

        Assert.False(follower.CatchUp(tampered));

        Assert.Equal(15, follower.Commands.Count);
    }

    [Theory]
    [InlineData("""{"formatVersion":99,"rulesVersion":"9.0.0","rulesConfigHash":"","setup":{"map":"riverford","seed":1,"seats":{"P1":"human","P2":"human"}},"commands":[]}""", "newer version")]
    [InlineData("""{"formatVersion":2,"rulesVersion":"0.7.0","rulesConfigHash":"","commands":[]}""", "no setup")]
    [InlineData("""{"formatVersion":2,"rulesVersion":"0.7.0","rulesConfigHash":"","setup":{"map":"riverford","seed":1,"seats":{}},"commands":[]}""", "no setup")]
    [InlineData("""{"formatVersion":2,"rulesVersion":"0.7.0","rulesConfigHash":"","setup":{"map":"riverford","seed":1,"seats":{"P1":"human","P2":"human"}}}""", "no command list")]
    public void IncompleteOrNewerRecordsAreRejectedWithAPlainReason(string json, string reason)
    {
        JsonException ex = Assert.Throws<JsonException>(() => MatchRecord.FromJson(json, RulesConfig.Default));

        Assert.Contains(reason, ex.Message);
    }

    private static string Json<T>(T value) => JsonSerializer.Serialize(value, CoreJson.Options);

    /// <summary>The record as if the rules had changed after it was made: a recorded hash no longer matches.</summary>
    private static MatchRecord Drift(MatchRecord record) => record with
    {
        Commands = [.. record.Commands.Select(c => c.Seq == 10 ? c with { StateHashAfter = "drifted" } : c)],
    };
}
