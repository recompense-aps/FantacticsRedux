using System.Collections.Immutable;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Geometry;
using Fantactics.Core.State;

namespace Fantactics.Client.Logic.Input;

/// <summary>
/// Builds a seat's starting placement from its <see cref="PlacementOptions"/> (GameDesign §4.4): pick a unit, then a
/// tile in the deploy zone. Picking a tile another unit stands on swaps them. After each placement the next unplaced
/// unit is picked, so placing an army is one click per unit.
/// </summary>
public sealed class PlacementBuilder
{
    private readonly ImmutableHashSet<Point> _tiles;
    private readonly Dictionary<int, Point> _placed = [];
    private readonly Dictionary<int, string> _types;

    /// <summary>Starts with nothing placed and the first unit picked.</summary>
    /// <param name="view">The seat's view (for unit types).</param>
    /// <param name="options">The units to place and the legal tiles.</param>
    public PlacementBuilder(PlayerView view, PlacementOptions options)
    {
        Options = options;
        _tiles = [.. options.Tiles];
        _types = options.UnitIds.ToDictionary(
            id => id,
            id => view.Units.FirstOrDefault(unit => unit.Id == id)?.Type ?? $"Unit {id}");
        Selected = Unplaced.Cast<int?>().FirstOrDefault();
    }

    /// <summary>The units to place and the legal tiles.</summary>
    public PlacementOptions Options { get; }

    /// <summary>The unit the next tile click places, if any.</summary>
    public int? Selected { get; private set; }

    /// <summary>Where each placed unit goes.</summary>
    public IReadOnlyDictionary<int, Point> Placed => _placed;

    /// <summary>Units not placed yet, in id order.</summary>
    public IEnumerable<int> Unplaced => Options.UnitIds.Where(id => !_placed.ContainsKey(id));

    /// <summary>Legal tiles no unit has taken.</summary>
    public IEnumerable<Point> FreeTiles => Options.Tiles.Where(tile => !_placed.ContainsValue(tile));

    /// <summary>Why the placement can't be submitted yet; empty when it can.</summary>
    public IReadOnlyList<string> Problems => Unplaced.Count() is int left and > 0
        ? [left == 1 ? "1 unit still to place." : $"{left} units still to place."]
        : [];

    /// <summary>A unit's type.</summary>
    public string TypeOf(int unitId) => _types.GetValueOrDefault(unitId, $"Unit {unitId}");

    /// <summary>The unit placed on <paramref name="tile"/>, if any.</summary>
    public int? UnitAt(Point tile) => _placed
        .Where(pair => pair.Value == tile)
        .Select(pair => (int?)pair.Key)
        .FirstOrDefault();

    /// <summary>Picks a unit to place (or move).</summary>
    /// <returns>Whether it's one of the units to place.</returns>
    public bool Select(int unitId)
    {
        if (!_types.ContainsKey(unitId))
        {
            return false;
        }

        Selected = unitId;
        return true;
    }

    /// <summary>Drops the pick.</summary>
    public void Deselect() => Selected = null;

    /// <summary>Places the picked unit on <paramref name="tile"/>, swapping with a unit already there.</summary>
    /// <returns>Why it couldn't be placed, or <c>null</c> if it was.</returns>
    public string? Choose(Point tile)
    {
        if (Selected is not int unitId)
        {
            return "Pick a unit to place first.";
        }

        if (!_tiles.Contains(tile))
        {
            return "Units start in your deploy zone (highlighted).";
        }

        if (UnitAt(tile) is int other && other != unitId)
        {
            if (_placed.TryGetValue(unitId, out Point from))
            {
                _placed[other] = from;
            }
            else
            {
                _placed.Remove(other);
            }
        }

        _placed[unitId] = tile;
        Selected = Unplaced.Cast<int?>().FirstOrDefault();
        return null;
    }

    /// <summary>Takes a unit off the board.</summary>
    public void Clear(int unitId)
    {
        _placed.Remove(unitId);
        Selected = unitId;
    }

    /// <summary>Replaces the placement with <paramref name="placement"/> (e.g. a bot's suggestion), keeping what fits.</summary>
    public void Load(PlaceStartingArmy placement)
    {
        _placed.Clear();
        foreach (UnitPlacement p in placement.Placements
            .Where(p => _types.ContainsKey(p.UnitId) && _tiles.Contains(p.Tile)))
        {
            if (UnitAt(p.Tile) is null)
            {
                _placed[p.UnitId] = p.Tile;
            }
        }

        Selected = Unplaced.Cast<int?>().FirstOrDefault();
    }

    /// <summary>The placement as a command.</summary>
    public PlaceStartingArmy Build() => new([.. _placed
        .OrderBy(pair => pair.Key)
        .Select(pair => new UnitPlacement(pair.Key, pair.Value))]);
}
