using System.Collections.Immutable;
using Fantactics.Client.Logic.Board;
using Fantactics.Core;
using Fantactics.Core.Events;
using Fantactics.Core.Geometry;
using Fantactics.Core.State;

namespace Fantactics.Client.Logic.Log;

/// <summary>
/// Turns events into lines for the player's log, e.g. <c>P1 Archer hits P2 Grunt for 3, 4 HP left</c>. A run of
/// movement steps becomes one line per unit, and a run of placements one line per seat; events in
/// <see cref="Ignored"/> show nothing.
/// </summary>
public static class EventText
{
    /// <summary>
    /// Event types the log leaves out: bookkeeping a player doesn't need to read, and waits, which auto-skip makes
    /// for most units every turn.
    /// </summary>
    public static ImmutableHashSet<Type> Ignored { get; } =
    [
        typeof(InitiativeOrdered),
        typeof(TurnEnded),
        typeof(UnitStopped),
        typeof(UnitWaited),
    ];

    /// <summary>Event types that are summed up over a run of them (see <see cref="Lines"/>), not described alone.</summary>
    public static ImmutableHashSet<Type> Summarized { get; } =
    [
        typeof(UnitPlaced),
        typeof(UnitStepped),
    ];

    /// <summary>The lines for one update's events, in order.</summary>
    /// <param name="events">The events.</param>
    /// <param name="names">Unit names; units that appear are learned into it.</param>
    /// <param name="turn">The turn before the events (each <see cref="TurnStarted"/> moves it on).</param>
    public static IReadOnlyList<LogLine> Lines(IEnumerable<GameEvent> events, UnitNames names, int turn)
    {
        List<LogLine> lines = [];
        Dictionary<int, (Point From, Point To)> moves = [];
        Dictionary<Seat, int> placed = [];
        foreach (GameEvent gameEvent in events)
        {
            switch (gameEvent)
            {
                case UnitPlaced e:
                    names.Learn(e.UnitId, e.Owner, e.Type);
                    placed[e.Owner] = placed.GetValueOrDefault(e.Owner) + 1;
                    continue;
                case UnitStepped e:
                    moves[e.UnitId] = moves.TryGetValue(e.UnitId, out (Point From, Point To) move)
                        ? (move.From, e.To)
                        : (e.From, e.To);
                    continue;
                case UnitArrived e:
                    names.Learn(e.UnitId, e.Owner, e.Type);
                    break;
                case UnitSummoned e:
                    names.Learn(e.UnitId, e.Owner, e.Type);
                    break;
            }

            if (Describe(gameEvent, names) is string line)
            {
                Flush(lines, moves, placed, names, turn);
                turn = gameEvent is TurnStarted started ? started.Turn : turn;
                lines.Add(new LogLine(turn, line, Heading: gameEvent is TurnStarted));
            }
        }

        Flush(lines, moves, placed, names, turn);
        return lines;
    }

    /// <summary>
    /// One event's line, or <c>null</c> for an <see cref="Ignored"/> or <see cref="Summarized"/> event.
    /// </summary>
    /// <exception cref="NotSupportedException">A new event type has no text yet.</exception>
    public static string? Describe(GameEvent gameEvent, UnitNames names) => gameEvent switch
    {
        UnitAttacked e => $"{names[e.AttackerId]} hits {names[e.TargetId]} for {e.Damage}{Kind(e.Kind)}, "
            + $"{e.HpAfter} HP left",
        UnitDied e => $"{names[e.UnitId]} is destroyed by {names[e.KillerId]}",
        UnitHealed e => $"{names[e.UnitId]} heals {e.Amount} ({e.HpAfter} HP)",
        UnitArrived e => $"{names[e.UnitId]} deploys at {e.Tile} for {e.CommandSpent} Command"
            + (e.CanAct ? "" : ", can't act this turn"),
        UnitSummoned e => $"{names[e.UnitId]} is summoned at {e.Tile}",
        ClashMarked e => $"{names[e.UnitA]} and {names[e.UnitB]} clash"
            + (e.Tile is Point tile ? $" at {tile}" : " (swapping places)"),
        ClashResolved { WinnerId: int winner } e =>
            $"{names[winner]} wins the clash with {names[winner == e.UnitA ? e.UnitB : e.UnitA]}",
        ClashResolved e => $"The clash between {names[e.UnitA]} and {names[e.UnitB]} ends with both standing",
        ClashAvoided e => $"{names[e.UnitId]} avoids a clash with {names[e.EnemyId]}",
        StatusApplied e => $"{names[e.UnitId]} is {e.Status} through turn {e.LastsThroughTurn}",
        StatusRemoved e => $"{names[e.UnitId]} is no longer {e.Status}",
        AbilityUsed e => $"{names[e.UnitId]} uses {UnitText.Words(e.Ability ?? "an ability")}"
            + (e.Target is Point target ? $" on {target}" : ""),
        UnitDelayed e => $"{names[e.UnitId]} delays",
        TileChanged e => $"{e.Tile} becomes {e.Terrain}",
        CommandGained e => $"{e.Seat} gains {e.Amount} Command ({e.Total} total)",
        ObjectiveScored e => $"{e.Seat} scores {e.Points} for holding {e.Held} objective{(e.Held == 1 ? "" : "s")} "
            + $"({e.Total} total)",
        OrdersLocked e => $"{e.Seat} locked in {Orders(e.Phase)}",
        TurnStarted e => $"Turn {e.Turn}",
        SeatEliminated e => $"{e.Seat} is eliminated",
        MatchEnded e => $"{MatchOutcome.Headline(e.Winners)} ({UnitText.Words(e.Reason.ToString())})",
        _ when Ignored.Contains(gameEvent.GetType()) || Summarized.Contains(gameEvent.GetType()) => null,
        _ => throw new NotSupportedException($"No log text for {gameEvent.GetType().Name}."),
    };

    /// <summary>The line announcing <paramref name="phase"/>, or <c>null</c> for phases with nothing to announce.</summary>
    public static string? PhaseLine(Phase phase) => phase switch
    {
        Phase.Placement or Phase.Movement or Phase.Action => $"{phase} phase",
        _ => null,
    };

    private static string Kind(AttackKind kind) => kind switch
    {
        AttackKind.PointBlank => " (point blank)",
        AttackKind.Clash => " (clash)",
        AttackKind.Retaliate => " (retaliate)",
        _ => "",
    };

    private static string Orders(Phase phase) => phase switch
    {
        Phase.Draft => "their draft",
        Phase.Placement => "their placement",
        Phase.Movement => "move orders",
        _ => "orders",
    };

    /// <summary>Writes out the pending run of moves and placements.</summary>
    private static void Flush(
        List<LogLine> lines,
        Dictionary<int, (Point From, Point To)> moves,
        Dictionary<Seat, int> placed,
        UnitNames names,
        int turn)
    {
        lines.AddRange(placed.Select(pair =>
            new LogLine(turn, $"{pair.Key} places {pair.Value} unit{(pair.Value == 1 ? "" : "s")}")));
        lines.AddRange(moves
            .Where(pair => pair.Value.From != pair.Value.To)
            .Select(pair => new LogLine(turn, $"{names[pair.Key]} moves {pair.Value.From} → {pair.Value.To}")));
        placed.Clear();
        moves.Clear();
    }
}
