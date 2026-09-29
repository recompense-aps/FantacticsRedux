using System.Collections.Immutable;
using Fantactics.Core;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Geometry;
using Fantactics.Core.Rules;
using Fantactics.Core.State;
using Fantactics.Sim.Matches;

namespace Fantactics.Sim.Views;

/// <summary>Builds <see cref="LegalView"/>: the seat's options plus how to submit them.</summary>
public static class LegalBuilder
{
    /// <summary>Usage for movement orders.</summary>
    public const string MovesUsage =
        "act --orders \"A>5,3 B>3,1>5,3 C=hold D@1,7\": move along the cheapest path (optional waypoints), hold, "
        + "or deploy a reserve unit; units without a clause hold";

    /// <summary>Builds the options for the decision <paramref name="viewer"/> owes, or <c>null</c> if none.</summary>
    /// <param name="session">The match.</param>
    /// <param name="viewer">The seat.</param>
    /// <param name="unitFilter">For movement, only list this unit's destinations.</param>
    public static LegalView? Build(MatchSession session, Seat viewer, string? unitFilter = null)
    {
        GameState state = session.State;
        UnitHandles handles = session.HandlesFor(viewer);
        return LegalActions.For(state, viewer) switch
        {
            null => null,
            { Decision: ChooseUnitActionDecision action } legal => new LegalView(
                "Action",
                handles.Of(action.UnitId),
                "act --pick N",
                Options: [.. legal.Actions.Select((option, index) => ActionRow(state, handles, option, index + 1))]),
            { Moves: MoveOptions moves } => new LegalView(
                "Moves",
                null,
                MovesUsage,
                Reach: [.. Reach(moves, handles, unitFilter)],
                Deploys: [.. moves.Deploys.Select(d => new DeployRow(
                    handles.Of(d.UnitId),
                    d.Type,
                    d.Cost,
                    string.Join(" ", d.Tiles.Select(EventFormatter.Tile))))],
                Command: moves.Command,
                MaxArrivals: moves.MaxArrivals),
            { Draft: DraftOptions draft } => new LegalView(
                "Draft",
                null,
                "act --draft \"Archer Archer Tank | Scout\": starting units, then | and reserve units, any races",
                Draftable: [.. draft.Units.Select(option => DraftRow(state.Rules, option))],
                Budget: draft.Budget,
                StartingCap: draft.StartingCap),
            { Placement: PlacementOptions placement } => new LegalView(
                "Placement",
                null,
                "act --orders \"A@0,5 B@1,6\": place every listed unit on its own tile",
                ToPlace: [.. placement.UnitIds.Select(id => $"{handles.Of(id)} {state.Units[id].Type}")],
                PlaceTiles: ZoneDescription(placement.Tiles)),
            _ => null,
        };
    }

    private static ActionRow ActionRow(GameState state, UnitHandles handles, ActionOption option, int pick)
    {
        (string action, Point? tile) = option.Command switch
        {
            Attack attack => ("attack", state.Units[attack.TargetId].Position),
            UseAbility ability => (ability.Ability, ability.Target),
            Wait => ("wait", null),
            Delay => ("delay", null),
            _ => (option.Command.GetType().Name, (Point?)null),
        };
        string target = tile is Point at && state.UnitAt(at) is Unit unit ? handles.Of(unit.Id) : "";
        return new ActionRow(
            pick,
            action,
            target,
            tile is Point t ? EventFormatter.Tile(t) : "",
            option.Preview?.Damage ?? 0,
            option.Preview?.Kills ?? false);
    }

    private static IEnumerable<ReachRow> Reach(MoveOptions moves, UnitHandles handles, string? unitFilter) =>
        moves.Units
            .Where(unit => unitFilter is null || handles.Of(unit.UnitId) == unitFilter)
            .SelectMany(unit => unit.Destinations.Select(destination => new ReachRow(
                handles.Of(unit.UnitId),
                destination.Tile.X,
                destination.Tile.Y,
                destination.Cost)));

    private static DraftRow DraftRow(RulesConfig rules, DraftUnitOption option)
    {
        UnitDefinition definition = rules.Units[option.Type];
        IEnumerable<string> traits = rules.TraitsOf(option.Type)
            .Select(pair => pair.Value > 1 ? $"{pair.Key}{pair.Value}" : pair.Key)
            .Concat(definition.Abilities);
        return new DraftRow(
            option.Type,
            option.Race,
            option.Cost,
            definition.Hp,
            definition.Attack,
            definition.Defense,
            definition.Movement,
            definition.MinRange == definition.MaxRange
                ? $"{definition.MaxRange}"
                : $"{definition.MinRange}-{definition.MaxRange}",
            definition.Initiative,
            option.Unique,
            string.Join(" ", option.Classes),
            string.Join(" ", traits));
    }

    private static string ZoneDescription(ImmutableArray<Point> tiles) =>
        $"any of these {tiles.Length} tiles: columns {tiles.Min(t => t.X)}-{tiles.Max(t => t.X)}, "
        + $"rows {tiles.Min(t => t.Y)}-{tiles.Max(t => t.Y)}, except impassable ones";
}
