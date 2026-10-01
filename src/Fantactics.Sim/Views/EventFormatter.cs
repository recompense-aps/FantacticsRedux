using System.Collections.Immutable;
using System.Text;
using Fantactics.Core.Events;
using Fantactics.Core.Geometry;
using Fantactics.Core.Records;
using Fantactics.Sim.Matches;

namespace Fantactics.Sim.Views;

/// <summary>
/// Flattens engine events to <see cref="EventLine"/>s from one seat's point of view. A unit's movement ticks within
/// one command collapse into a single <c>move</c> line with its path, which keeps logs short.
/// </summary>
public static class EventFormatter
{
    /// <summary>Formats events in order.</summary>
    public static ImmutableArray<EventLine> Format(IEnumerable<LoggedEvent> events, UnitHandles handles)
    {
        List<EventLine> lines = [];
        Dictionary<(int Seq, int Unit), (int Index, StringBuilder Path)> moves = [];
        foreach (LoggedEvent logged in events)
        {
            if (logged.Event is UnitStepped { Tick: > 0 } step)
            {
                if (moves.TryGetValue((logged.Seq, step.UnitId), out (int Index, StringBuilder Path) move))
                {
                    move.Path.Append('>').Append(Tile(step.To));
                    lines[move.Index] = lines[move.Index] with { Detail = move.Path.ToString() };
                }
                else
                {
                    StringBuilder path = new StringBuilder(Tile(step.From)).Append('>').Append(Tile(step.To));
                    moves[(logged.Seq, step.UnitId)] = (lines.Count, path);
                    lines.Add(Line(logged, "move", handles.Of(step.UnitId), "", path.ToString()));
                }

                continue;
            }

            lines.Add(Describe(logged, handles));
        }

        return [.. lines];
    }

    /// <summary>A tile as <c>x,y</c>, the same form the orders grammar uses.</summary>
    public static string Tile(Point point) => $"{point.X},{point.Y}";

    private static EventLine Describe(LoggedEvent logged, UnitHandles h) => logged.Event switch
    {
        OrdersLocked e => Line(logged, "locked", e.Seat.ToString(), "", $"{e.Phase} orders in"),
        UnitPlaced e => Line(logged, "placed", h.Of(e.UnitId), "", $"{e.Type} at {Tile(e.Tile)}"),
        TurnStarted e => Line(logged, "turn", "", "", $"turn {e.Turn} starts; tie priority {e.TiePriority}"),
        CommandGained e => Line(logged, "command", e.Seat.ToString(), "", $"+{e.Amount} = {e.Total}"),
        UnitArrived e => Line(
            logged,
            "arrived",
            h.Of(e.UnitId),
            "",
            $"{e.Type} at {Tile(e.Tile)} for {e.CommandSpent} Command" + (e.CanAct ? "" : "; can't act this turn")),
        UnitStepped e => Line(logged, "take", h.Of(e.UnitId), "", $"{Tile(e.From)}>{Tile(e.To)} after clash"),
        UnitStopped e => Line(logged, "stopped", h.Of(e.UnitId), "", $"{e.Reason} at {Tile(e.Tile)}"),
        ClashMarked e => Line(
            logged,
            "clash",
            h.Of(e.UnitA),
            h.Of(e.UnitB),
            e.Tile is Point tile ? $"both entered {Tile(tile)}" : "tried to swap tiles"),
        ClashAvoided e => Line(logged, "slipped", h.Of(e.UnitId), h.Of(e.EnemyId), "retreated instead of clashing"),
        ClashResolved { WinnerId: int winner } e => Line(
            logged,
            "clash-won",
            h.Of(winner),
            h.Of(winner == e.UnitA ? e.UnitB : e.UnitA),
            "winner takes the tile and gets no action"),
        ClashResolved e => Line(logged, "clash-draw", h.Of(e.UnitA), h.Of(e.UnitB), "strike cap reached"),
        UnitAttacked e => Line(
            logged,
            e.Kind switch
            {
                AttackKind.PointBlank => "point-blank",
                AttackKind.Clash => "clash-strike",
                AttackKind.Retaliate => "retaliate",
                _ => "attack",
            },
            h.Of(e.AttackerId),
            h.Of(e.TargetId),
            $"{e.Damage} dmg, {e.HpAfter} hp left"),
        UnitHealed e => Line(logged, "healed", h.Of(e.UnitId), "", $"+{e.Amount} = {e.HpAfter} hp"),
        StatusApplied e => Line(logged, "status", h.Of(e.UnitId), "", $"{e.Status} through turn {e.LastsThroughTurn}"),
        StatusRemoved e => Line(logged, "status-end", h.Of(e.UnitId), "", $"{e.Status} ended"),
        UnitDied e => Line(logged, "died", h.Of(e.UnitId), h.Of(e.KillerId), $"worth {e.Value}"),
        InitiativeOrdered e => Line(logged, "order", "", "", string.Join(" ", e.Order.Select(h.Of))),
        UnitWaited e => Line(logged, "wait", h.Of(e.UnitId), "", ""),
        UnitDelayed e => Line(logged, "delay", h.Of(e.UnitId), "", "moved to the end of the order"),
        AbilityUsed e => Line(
            logged,
            "ability",
            h.Of(e.UnitId),
            "",
            e.Target is Point target ? $"{e.Ability} at {Tile(target)}" : e.Ability),
        TileChanged e => Line(logged, "terrain", "", "", $"{Tile(e.Tile)} is now {e.Terrain}"),
        UnitSummoned e => Line(logged, "summoned", h.Of(e.UnitId), "", $"{e.Type} at {Tile(e.Tile)}; can't act this turn"),
        ObjectiveScored e => Line(
            logged,
            "objective",
            e.Seat.ToString(),
            "",
            $"held {e.Held} vs {e.EnemyHeld} objective tiles: +{e.Points} = {e.Total}"),
        TurnEnded e => Line(logged, "turn-end", "", "", $"turn {e.Turn} ends"),
        SeatEliminated e => Line(logged, "eliminated", e.Seat.ToString(), "", "routed; its units leave the field"),
        MatchEnded e => Line(logged, "match-end", string.Join('+', e.Winners), "", $"{e.Reason}"),
        _ => Line(logged, logged.Event.GetType().Name, "", "", ""),
    };

    private static EventLine Line(LoggedEvent logged, string type, string actor, string target, string detail) =>
        new(logged.Seq, logged.Turn, type, actor, target, detail);
}
