using System.Collections.Immutable;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Geometry;
using Fantactics.Core.State;

namespace Fantactics.Client.Logic.Input;

/// <summary>
/// Builds a seat's movement-phase orders from clicks: select a unit, pick a highlighted destination (the engine's
/// cheapest path), or pick an arrival tile for a reserve unit. Units without an order hold. Everything it builds
/// comes from <see cref="MoveOptions"/>, so single orders are always legal; <see cref="Problems"/> reports the joint
/// constraints (a unit ending where a holding ally stands) before submitting.
/// </summary>
public sealed class MoveOrderBuilder
{
    private readonly PlayerView _view;
    private readonly MoveOptions _options;
    private readonly Dictionary<int, ReachableTile> _moves = [];
    private readonly Dictionary<int, Point> _deploys = [];

    /// <summary>Starts with every unit holding and nothing deployed.</summary>
    /// <param name="view">The seat's view.</param>
    /// <param name="options">The seat's move options.</param>
    public MoveOrderBuilder(PlayerView view, MoveOptions options)
    {
        _view = view;
        _options = options;
    }

    /// <summary>The selected unit (on the field or in reserve), if any.</summary>
    public int? Selected { get; private set; }

    /// <summary>Destinations chosen so far, by unit.</summary>
    public IReadOnlyDictionary<int, ReachableTile> Moves => _moves;

    /// <summary>Arrival tiles chosen so far, by reserve unit.</summary>
    public IReadOnlyDictionary<int, Point> Deploys => _deploys;

    /// <summary>Reserve units that can be deployed this turn.</summary>
    public ImmutableArray<DeployOption> DeployOptions => _options.Deploys;

    /// <summary>Command left after the chosen deploys.</summary>
    public int CommandLeft => _options.Command - _deploys.Keys.Sum(id => DeployOf(id).Cost);

    /// <summary>Whether <see cref="Selected"/> is a reserve unit.</summary>
    public bool IsDeploying => Selected is int id && _options.Deploys.Any(d => d.UnitId == id);

    /// <summary>Tiles the selected unit can be sent to (destinations, or free arrival tiles).</summary>
    public IEnumerable<Point> Targets => Selected switch
    {
        int id when IsDeploying => DeployOf(id).Tiles.Where(tile => !_deploys.Any(d => d.Key != id && d.Value == tile)),
        int id => MovesOf(id)?.Destinations.Select(d => d.Tile) ?? [],
        _ => [],
    };

    /// <summary>Why the orders can't be submitted together yet; empty when they can.</summary>
    public IReadOnlyList<string> Problems => [.. _moves
        .Where(move => HolderAt(move.Value.Tile) is Unit holder && holder.Id != move.Key)
        .Select(move => $"Unit {move.Key} would end on {move.Value.Tile}, where unit {HolderAt(move.Value.Tile)?.Id} holds.")];

    /// <summary>Selects one of the seat's units that can move, or a reserve unit that can deploy.</summary>
    /// <returns>Whether the unit can be given an order.</returns>
    public bool Select(int unitId)
    {
        bool selectable = MovesOf(unitId) is { Destinations.Length: > 0 } || _options.Deploys.Any(d => d.UnitId == unitId);
        Selected = selectable ? unitId : null;
        return selectable;
    }

    /// <summary>Clears the selection.</summary>
    public void Deselect() => Selected = null;

    /// <summary>The path the selected unit would take to <paramref name="tile"/>, or empty if it can't go there.</summary>
    public ImmutableArray<Point> Preview(Point tile) =>
        Selected is int id && !IsDeploying && MovesOf(id)?.Destinations.FirstOrDefault(d => d.Tile == tile) is ReachableTile reach
            ? reach.Path
            : [];

    /// <summary>
    /// Sends the selected unit to <paramref name="tile"/>: a destination, its own tile (hold), or an arrival tile.
    /// The selection is cleared when it works.
    /// </summary>
    /// <returns><c>null</c> if the order was set, otherwise why not.</returns>
    public string? Choose(Point tile)
    {
        if (Selected is not int id)
        {
            return "Select a unit first.";
        }

        string? problem = IsDeploying ? ChooseArrival(id, tile) : ChooseDestination(id, tile);
        if (problem is null)
        {
            Selected = null;
        }

        return problem;
    }

    /// <summary>Removes a unit's order: a field unit holds, a reserve unit stays in reserve.</summary>
    public void Clear(int unitId)
    {
        _moves.Remove(unitId);
        _deploys.Remove(unitId);
    }

    /// <summary>The orders as a command, in the seat's own ids.</summary>
    public SubmitMoveOrders Build() => new(
        [.. _moves.OrderBy(move => move.Key).Select(move => new MoveOrder(move.Key, move.Value.Path))],
        [.. _deploys.OrderBy(deploy => deploy.Key).Select(deploy => new DeployOrder(deploy.Key, deploy.Value))]);

    private string? ChooseDestination(int id, Point tile)
    {
        Unit? unit = _view.Units.FirstOrDefault(u => u.Id == id);
        if (unit?.Position == tile)
        {
            _moves.Remove(id);
            return null;
        }

        if (MovesOf(id)?.Destinations.FirstOrDefault(d => d.Tile == tile) is not ReachableTile reach)
        {
            return $"Unit {id} can't reach {tile} this turn.";
        }

        _moves[id] = reach;
        return null;
    }

    private string? ChooseArrival(int id, Point tile)
    {
        DeployOption option = DeployOf(id);
        int costIfChosen = _deploys.ContainsKey(id) ? CommandLeft : CommandLeft - option.Cost;
        string? problem = true switch
        {
            _ when !option.Tiles.Contains(tile) => $"Unit {id} can't arrive on {tile}.",
            _ when _deploys.Any(d => d.Key != id && d.Value == tile) => $"Another unit already arrives on {tile}.",
            _ when !_deploys.ContainsKey(id) && _deploys.Count >= _options.MaxArrivals =>
                $"At most {_options.MaxArrivals} units can arrive per turn.",
            _ when costIfChosen < 0 => $"Deploying {option.Type} costs {option.Cost} Command; you have {CommandLeft}.",
            _ => null,
        };
        if (problem is null)
        {
            _deploys[id] = tile;
        }

        return problem;
    }

    private UnitMoveOptions? MovesOf(int unitId) => _options.Units.FirstOrDefault(u => u.UnitId == unitId);

    private DeployOption DeployOf(int unitId) => _options.Deploys.First(d => d.UnitId == unitId);

    /// <summary>The seat's unit that will still stand on <paramref name="tile"/> after moving (it has no order), if any.</summary>
    private Unit? HolderAt(Point tile) => _view.Units.FirstOrDefault(unit =>
        unit.Owner == _view.Seat && unit.IsOnField && unit.Position == tile && !_moves.ContainsKey(unit.Id));
}
