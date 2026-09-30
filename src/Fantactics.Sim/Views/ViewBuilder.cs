using System.Collections.Immutable;
using Fantactics.Core;
using Fantactics.Core.Engine;
using Fantactics.Core.Geometry;
using Fantactics.Core.Maps;
using Fantactics.Core.Rules;
using Fantactics.Core.State;
using Fantactics.Sim.Matches;

namespace Fantactics.Sim.Views;

/// <summary>Builds <see cref="SeatView"/> and <see cref="StatusView"/>. Only reads what the seat may see.</summary>
public static class ViewBuilder
{
    /// <summary>How to read the map and unit ids.</summary>
    public const string Legend =
        "map x=column y=row from top-left; terrain . plains = road % forest + hills ^ mountains # bridge ~ water; "
        + "* = objective tile (hold the most of them to score each turn); "
        + "units UPPER=yours lower=other players' (the units table says enemy or ally) (@ = unit with a 2-letter id)";

    private const int RecentLimit = 60;

    /// <summary>Builds the view for <paramref name="viewer"/>.</summary>
    /// <param name="session">The match.</param>
    /// <param name="viewer">The viewing seat.</param>
    /// <param name="includeMap">Whether to include the map rows.</param>
    public static SeatView Build(MatchSession session, Seat viewer, bool includeMap)
    {
        GameState state = session.State;
        PlayerView view = PlayerView.Project(state, viewer);
        UnitHandles handles = session.HandlesFor(viewer);
        ViewIds ids = ViewIds.For(state, viewer);

        // The view carries per-player ids; handles are keyed by engine ids.
        string Handle(int viewId) => handles.Of(ids.ToEngine(viewId));

        ImmutableArray<UnitRow> units = view.Units
            .Where(unit => unit.IsOnField)
            .OrderBy(unit => unit.Owner == viewer ? 0 : 1)
            .ThenBy(unit => Handle(unit.Id).Length)
            .ThenBy(unit => Handle(unit.Id), StringComparer.Ordinal)
            .Select(unit => Row(state, view, unit, Handle))
            .ToImmutableArray();
        ImmutableArray<ReserveRow> reserve = view.Units
            .Where(unit => unit.Owner == viewer && !unit.IsOnField)
            .Select(unit => new ReserveRow(
                Handle(unit.Id),
                unit.Type,
                unit.Location == UnitLocation.Reserve ? "reserve" : "unplaced",
                state.DefinitionOf(unit).Cost,
                UnitRules.DeployCost(state, unit)))
            .ToImmutableArray();

        return new SeatView(
            view.Turn,
            view.Phase.ToString(),
            viewer.ToString(),
            view.TiePriority.ToString(),
            Side(view.Players[viewer]),
            [.. view.Opponents().Select(seat => Side(view.Players[seat]))],
            [.. view.Players.Values
                .Where(player => player.Seat != viewer && !view.AreEnemies(player.Seat, viewer))
                .Select(Side)],
            Pending(GameEngine.PendingDecisionFor(state, viewer), handles),
            WaitingFor(view),
            Legend,
            includeMap ? Rows(view, viewer, Handle) : null,
            units,
            reserve,
            Order(view, Handle),
            Recent(session, viewer, handles),
            Outcome(view.Outcome));
    }

    /// <summary>Builds the public status of the match.</summary>
    public static StatusView Status(MatchSession session)
    {
        GameState state = session.State;
        ImmutableArray<SeatStatus> seats = state.Players.Values
            .Select(player => new SeatStatus(
                player.Seat.ToString(),
                session.KindOf(player.Seat).Value,
                player.TeamNumber,
                RaceText.Allowed(player.AllowedRaces),
                $"{player.BudgetUnder(state.Rules)}/{player.StartingCapUnder(state.Rules)}",
                player.Eliminated ? "out"
                    : GameEngine.PendingDecisionFor(state, player.Seat) is Decision decision ? KindOf(decision)
                    : ""))
            .ToImmutableArray();
        return new StatusView(state.Turn, state.Phase.ToString(), seats, session.CommandCount, Outcome(state.Outcome));
    }

    /// <summary>Short name of a decision: Draft, Placement, Moves, or Action.</summary>
    public static string KindOf(Decision decision) => decision switch
    {
        DraftArmyDecision => "Draft",
        PlaceStartingArmyDecision => "Placement",
        SubmitMoveOrdersDecision => "Moves",
        _ => "Action",
    };

    /// <summary>Seats that owe a decision.</summary>
    public static ImmutableArray<string> WaitingFor(PlayerView view) =>
        view.PendingDecisions.Select(d => d.Seat.ToString()).Distinct().ToImmutableArray();

    /// <summary>Readable result, e.g. <c>P2 wins (Rout)</c> or <c>P1 and P3 win (TurnLimit)</c>.</summary>
    public static string? Outcome(MatchOutcome? outcome) => outcome switch
    {
        null => null,
        { IsDraw: true } => $"draw ({outcome.Reason})",
        _ => $"{outcome.Headline()} ({outcome.Reason})",
    };

    private static UnitRow Row(GameState state, PlayerView view, Unit unit, Func<int, string> handle)
    {
        UnitDefinition definition = state.DefinitionOf(unit);
        IEnumerable<string> statuses = unit.Statuses
            .Select(pair => $"{pair.Key.ToString().ToLowerInvariant()}:{pair.Value}");
        IEnumerable<string> flags = new (bool On, string Flag)[]
            {
                (view.TurnState.Braced.Contains(unit.Id), "braced"),
                (view.TurnState.Held.Contains(unit.Id) && !view.TurnState.Braced.Contains(unit.Id), "held"),
                (view.TurnState.ClashWinners.Contains(unit.Id), "clash-won"),
                (unit.CannotActOnTurn == view.Turn, "cant-act"),
                (unit.IsSummoned, "summoned"),
            }
            .Where(flag => flag.On)
            .Select(flag => flag.Flag);
        return new UnitRow(
            handle(unit.Id),
            SideOf(view, unit.Owner),
            unit.Type,
            unit.Position.X,
            unit.Position.Y,
            state.Map[unit.Position].ToString(),
            unit.Hp,
            definition.Hp,
            definition.Attack,
            definition.Defense,
            UnitRules.MovementPoints(state, unit),
            definition.MinRange == definition.MaxRange
                ? $"{definition.MaxRange}"
                : $"{definition.MinRange}-{definition.MaxRange}",
            view.TurnState.EffectiveInitiative.GetValueOrDefault(unit.Id, definition.Initiative),
            string.Join(" ", statuses.Concat(flags)));
    }

    /// <summary>Whose a unit is, as the viewer sees it; one-against-one keeps the short <c>enemy</c>.</summary>
    private static string SideOf(PlayerView view, Seat owner) =>
        owner == view.Seat ? "you"
        : view.Players.Count == 2 ? "enemy"
        : $"{owner} {(view.AreEnemies(owner, view.Seat) ? "enemy" : "ally")}";

    private static SideSummary Side(PlayerSummary player) => new(
        player.Seat.ToString(),
        RaceText.Drafted(player.DraftedRaces),
        player.Command,
        player.ArmyValue,
        player.ReserveValue,
        player.DestroyedValue,
        player.ObjectivePoints);

    private static PendingInfo? Pending(Decision? decision, UnitHandles handles) => decision switch
    {
        null => null,
        ChooseUnitActionDecision action => new PendingInfo(KindOf(action), handles.Of(action.UnitId)),
        _ => new PendingInfo(KindOf(decision), null),
    };

    private static ImmutableArray<string> Rows(PlayerView view, Seat viewer, Func<int, string> handle)
    {
        Dictionary<Point, char> overlay = view.Units
            .Where(unit => unit.IsOnField)
            .ToDictionary(
                unit => unit.Position,
                unit => handle(unit.Id) is { Length: 1 } letter ? letter[0] : '@');
        return Enumerable.Range(0, view.Map.Height)
            .Select(y => new string(Enumerable.Range(0, view.Map.Width)
                .Select(x => new Point(x, y))
                .Select(p => overlay.TryGetValue(p, out char unit) ? unit
                    : view.Map.Objectives.Contains(p) ? '*'
                    : TerrainChars.ToChar(view.Map[p]))
                .ToArray()))
            .ToImmutableArray();
    }

    private static string? Order(PlayerView view, Func<int, string> handle)
    {
        if (view.Phase != Phase.Action)
        {
            return null;
        }

        string queue = string.Join(" ", view.TurnState.ActionQueue.Select(handle));
        return view.TurnState.DelayedQueue.IsEmpty
            ? queue
            : $"{queue} | delayed: {string.Join(" ", view.TurnState.DelayedQueue.Select(handle))}";
    }

    private static ImmutableArray<EventLine> Recent(MatchSession session, Seat viewer, UnitHandles handles)
    {
        int since = Math.Max(1, session.LastSeqOf(viewer));
        ImmutableArray<EventLine> lines = EventFormatter.Format(session.Events.Where(e => e.Seq >= since), handles);
        return lines.Length > RecentLimit ? lines[^RecentLimit..] : lines;
    }
}
