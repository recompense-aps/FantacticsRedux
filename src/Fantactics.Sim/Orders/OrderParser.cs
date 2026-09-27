using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Fantactics.Core;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Geometry;
using Fantactics.Core.State;
using Fantactics.Sim.Matches;
using Fantactics.Sim.Views;

namespace Fantactics.Sim.Orders;

/// <summary>
/// Parses the compact order grammar (Simulation §6.1) into Core commands:
/// <c>A&gt;5,3</c> move (cheapest path), <c>A&gt;3,1&gt;5,3</c> move via waypoints, <c>A=hold</c>, and
/// <c>A@1,7</c> deploy or place. Clauses are separated by spaces.
/// </summary>
public static partial class OrderParser
{
    /// <summary>Parses movement-phase orders (moves, holds, reserve deploys).</summary>
    /// <exception cref="SimException">The syntax is wrong or a unit or path doesn't exist.</exception>
    public static SubmitMoveOrders ParseMoveOrders(string text, GameState state, Seat seat, UnitHandles handles)
    {
        List<MoveOrder> moves = [];
        List<DeployOrder> deploys = [];
        foreach (Clause clause in Clauses(text))
        {
            Unit unit = OwnUnit(clause.Handle, state, seat, handles);
            switch (clause.Kind)
            {
                case '=':
                    RequireHold(clause);
                    break;
                case '@':
                    deploys.Add(new DeployOrder(unit.Id, SinglePoint(clause)));
                    break;
                default:
                    RequireOnField(unit, clause);
                    moves.Add(new MoveOrder(unit.Id, ExpandPath(state, unit, clause)));
                    break;
            }
        }

        return new SubmitMoveOrders([.. moves], [.. deploys]);
    }

    /// <summary>Parses starting placements (<c>A@0,5 B@1,6</c>).</summary>
    /// <exception cref="SimException">The syntax is wrong or a unit doesn't exist.</exception>
    public static PlaceStartingArmy ParsePlacement(string text, GameState state, Seat seat, UnitHandles handles) =>
        new([.. Clauses(text).Select(clause => clause.Kind == '@'
            ? new UnitPlacement(OwnUnit(clause.Handle, state, seat, handles).Id, SinglePoint(clause))
            : throw BadOrders($"'{clause.Text}': placement uses A@x,y."))]);

    /// <summary>Parses a draft: starting unit types, then <c>|</c> and reserve types.</summary>
    /// <exception cref="SimException">The syntax is wrong.</exception>
    public static SubmitDraft ParseDraft(string text)
    {
        string[] parts = text.Split('|');
        if (parts.Length > 2)
        {
            throw BadOrders("Use at most one '|' between starting units and reserve units.");
        }

        return new SubmitDraft(Types(parts[0]), parts.Length == 2 ? Types(parts[1]) : []);
    }

    private static ImmutableArray<string> Types(string part) =>
        [.. part.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    private static IEnumerable<Clause> Clauses(string text)
    {
        string[] clauses = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (clauses.Length == 0)
        {
            throw BadOrders("No orders given.");
        }

        return clauses.Select(raw => ClauseRegex().Match(raw) is { Success: true } match
            ? new Clause(
                raw,
                match.Groups["handle"].Value,
                match.Groups["kind"].Value[0],
                match.Groups["rest"].Value)
            : throw BadOrders($"Can't read '{raw}'. Use A>x,y, A>x,y>x,y, A=hold, or A@x,y."));
    }

    private static Unit OwnUnit(string handle, GameState state, Seat seat, UnitHandles handles) =>
        handles.Resolve(handle) is int id && state.Units.TryGetValue(id, out Unit? unit) && unit.Owner == seat
            ? unit
            : throw BadOrders($"'{handle}' is not one of your living units.");

    private static void RequireHold(Clause clause)
    {
        if (!clause.Rest.Equals("hold", StringComparison.OrdinalIgnoreCase))
        {
            throw BadOrders($"'{clause.Text}': the only '=' order is =hold.");
        }
    }

    private static void RequireOnField(Unit unit, Clause clause)
    {
        if (!unit.IsOnField)
        {
            throw BadOrders($"'{clause.Text}': {clause.Handle} is not on the field; deploy it with {clause.Handle}@x,y.");
        }
    }

    private static Point SinglePoint(Clause clause) =>
        Points(clause.Rest, clause) is [Point point]
            ? point
            : throw BadOrders($"'{clause.Text}': give exactly one tile after @.");

    private static List<Point> Points(string text, Clause clause) =>
        text.Split('>')
            .Select(part => PointRegex().Match(part) is { Success: true } match
                ? new Point(int.Parse(match.Groups["x"].Value), int.Parse(match.Groups["y"].Value))
                : throw BadOrders($"'{clause.Text}': '{part}' is not a tile like 5,3."))
            .ToList();

    /// <summary>Joins cheapest-path segments through each waypoint. The engine validates the total cost.</summary>
    private static ImmutableArray<Point> ExpandPath(GameState state, Unit unit, Clause clause)
    {
        List<Point> waypoints = Points(clause.Rest, clause);
        if (waypoints is [Point only] && Pathfinder.PathTo(state, unit, only) is ImmutableArray<Point> direct)
        {
            return direct;
        }

        List<Point> path = [];
        Point from = unit.Position;
        foreach (Point waypoint in waypoints)
        {
            ImmutableArray<Point> segment = Pathfinder.CheapestPath(state, unit, from, waypoint)
                ?? throw BadOrders($"'{clause.Text}': no path from {EventFormatter.Tile(from)} to "
                    + $"{EventFormatter.Tile(waypoint)}.");
            path.AddRange(segment);
            from = waypoint;
        }

        return [.. path];
    }

    private static SimException BadOrders(string message) => new(message, ExitCodes.RuleViolation);

    [GeneratedRegex(@"^(?<handle>[A-Za-z]+)(?<kind>[>=@])(?<rest>.+)$")]
    private static partial Regex ClauseRegex();

    [GeneratedRegex(@"^\s*(?<x>\d+)\s*,\s*(?<y>\d+)\s*$")]
    private static partial Regex PointRegex();

    /// <summary>One parsed clause.</summary>
    private sealed record Clause(string Text, string Handle, char Kind, string Rest);
}
