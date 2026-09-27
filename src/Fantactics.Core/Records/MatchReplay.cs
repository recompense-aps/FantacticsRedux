using Fantactics.Core.Engine;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Core.Records;

/// <summary>Replays a match record by folding <see cref="GameEngine.Apply"/> over its commands.</summary>
public static class MatchReplay
{
    /// <summary>Replays <paramref name="record"/> and checks every recorded state hash.</summary>
    /// <param name="rules">Rules to replay with.</param>
    /// <param name="record">The record.</param>
    /// <returns>The final state and the first command whose hash didn't match, if any (rules drift).</returns>
    /// <exception cref="InvalidOperationException">A recorded command is now rejected.</exception>
    public static ReplayResult Run(RulesConfig rules, MatchRecord record)
    {
        GameState state = record.Setup.CreateInitialState(rules);
        int? driftAt = null;
        foreach (RecordedCommand entry in record.Commands)
        {
            state = GameEngine.Apply(state, entry.Seat, entry.Command) switch
            {
                Accepted accepted => accepted.State,
                Rejected rejected => throw new InvalidOperationException(
                    $"Command {entry.Seq} is now rejected: {rejected.Violation.Code}: {rejected.Violation.Message}"),
                _ => throw new InvalidOperationException("Unknown apply result."),
            };
            if (driftAt is null && StateHash.Compute(state) != entry.StateHashAfter)
            {
                driftAt = entry.Seq;
            }
        }

        return new ReplayResult(state, driftAt);
    }
}
