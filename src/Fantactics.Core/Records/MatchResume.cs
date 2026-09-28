using Fantactics.Core.Engine;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Core.Records;

/// <summary>
/// Decides where a loaded record continues from (TechnicalDesign §4). The history is kept when it replays with every
/// state hash matching and ends at the snapshot. Otherwise play continues from the snapshot and the history is
/// dropped: after a rules change (the history no longer replays), or after the snapshot was edited by hand.
/// </summary>
public static class MatchResume
{
    /// <summary>Resolves where <paramref name="record"/> continues from.</summary>
    /// <exception cref="MatchResumeException">The history doesn't replay and there's no snapshot.</exception>
    public static ResumePoint Resolve(RulesConfig rules, MatchRecord record)
    {
        GameState start = record.StartState(rules);
        List<LoggedEvent> events = [];
        (GameState? final, string? failure) = Replay(start, record, events);
        if (final is not null && (record.Snapshot is null || StateHash.Compute(record.Snapshot) == StateHash.Compute(final)))
        {
            return new ResumePoint(start, record.Start is not null, record.Commands, [.. events], final, null);
        }

        string warning = final is not null
            ? "The saved position was edited, so play continues from it without the earlier history."
            : $"{failure} Play continues from the saved position without the history.";
        return record.Snapshot is GameState snapshot
            ? new ResumePoint(snapshot, true, [], [], snapshot, warning)
            : throw new MatchResumeException(failure ?? "The record doesn't replay.");
    }

    private static (GameState? Final, string? Failure) Replay(GameState start, MatchRecord record, List<LoggedEvent> events)
    {
        GameState state = start;
        foreach (RecordedCommand entry in record.Commands)
        {
            if (GameEngine.Apply(state, entry.Seat, entry.Command) is not Accepted accepted)
            {
                return (null, $"Command {entry.Seq} in the record is now rejected. The record was made with rules "
                    + $"{record.RulesVersion}; these are {GameEngine.RulesVersion}.");
            }

            LoggedEvent.Append(events, entry.Seq, state.Turn, accepted.Events);
            state = accepted.State;
            if (StateHash.Compute(state) != entry.StateHashAfter)
            {
                return (null, $"Rules drift at command {entry.Seq}: the replayed state differs from the record "
                    + $"(recorded with rules {record.RulesVersion}, now {GameEngine.RulesVersion}).");
            }
        }

        return (state, null);
    }
}
