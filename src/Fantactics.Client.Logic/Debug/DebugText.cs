using System.Collections.Immutable;
using Fantactics.Client.Logic.Session;
using Fantactics.Core;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Events;
using Fantactics.Core.Records;
using Fantactics.Core.State;

namespace Fantactics.Client.Logic.Debug;

/// <summary>
/// Text for the debug panel: the command timeline, the event log, hidden information, and save-file summaries. It
/// shows engine ids and everything in the state, so it's for testing only, never for a player.
/// </summary>
public static class DebugText
{
    /// <summary>One entry per command, in order.</summary>
    public static ImmutableArray<TimelineEntry> Timeline(MatchRecord record, IReadOnlyList<LoggedEvent> events)
    {
        ILookup<int, LoggedEvent> bySeq = events.ToLookup(e => e.Seq);
        int turn = record.Start?.Turn ?? 0;
        List<TimelineEntry> entries = [];
        foreach (RecordedCommand command in record.Commands)
        {
            turn = bySeq[command.Seq].Select(e => e.Turn).DefaultIfEmpty(turn).First();
            entries.Add(new TimelineEntry(command.Seq, turn, command.Seat, Describe(command.Command)));
        }

        return [.. entries];
    }

    /// <summary>A short line for an event, e.g. <c>T3 #2: UnitAttacked AttackerId=4 TargetId=9 …</c>.</summary>
    public static string Line(LoggedEvent logged)
    {
        string text = logged.Event.ToString();
        int brace = text.IndexOf('{');
        string fields = brace < 0 ? "" : text[(brace + 1)..].Trim('}', ' ').Replace(" = ", "=").Replace(",", "");
        return $"T{logged.Turn} #{logged.Seq}: {logged.Event.GetType().Name} {fields}".TrimEnd();
    }

    /// <summary>What <paramref name="seat"/> can't see: the enemy's reserve and any orders already locked in.</summary>
    public static IEnumerable<string> Hidden(GameState state, Seat seat)
    {
        Seat enemy = seat.Opponent();
        string reserve = string.Join(", ", state.Units.Values
            .Where(u => u.Owner == enemy && u.Location == UnitLocation.Reserve)
            .Select(u => $"#{u.Id} {u.Type}"));
        yield return $"{enemy} reserve: {(reserve.Length == 0 ? "none" : reserve)}";
        foreach ((Seat owner, ICommand orders) in state.PendingOrders)
        {
            yield return $"{owner} locked in: {Describe(orders)}";
        }
    }

    /// <summary>A save file's one-line summary (turn, phase, seats), read without replaying it.</summary>
    public static string Summary(string path) => SaveSummary.Read(path).Text;

    private static string Describe(ICommand command) => command switch
    {
        SubmitDraft draft => $"draft {string.Join(" ", draft.Starting)} | {string.Join(" ", draft.Reserve)}",
        PlaceStartingArmy place => $"place {place.Placements.Length} units",
        SubmitMoveOrders orders => $"move {orders.Moves.Count(m => !m.Path.IsEmpty)} units, deploy {orders.Deploys.Length}",
        Attack attack => $"#{attack.UnitId} attacks #{attack.TargetId}",
        UseAbility use => $"#{use.UnitId} uses {use.Ability}" + (use.Target is { } target ? $" on {target}" : ""),
        Wait wait => $"#{wait.UnitId} waits",
        Delay delay => $"#{delay.UnitId} delays",
        _ => command.GetType().Name,
    };
}
