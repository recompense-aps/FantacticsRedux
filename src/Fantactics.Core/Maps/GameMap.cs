using System.Collections.Immutable;
using Fantactics.Core.Geometry;

namespace Fantactics.Core.Maps;

/// <summary>An immutable rectangular grid of terrain.</summary>
/// <param name="Name">Map identifier, e.g. <c>riverford</c>.</param>
/// <param name="Width">Number of columns.</param>
/// <param name="Height">Number of rows.</param>
/// <param name="Tiles">Terrain in row-major order.</param>
/// <param name="Objectives">Objective tiles scored by the game mode (GameDesign §4.5); may be empty.</param>
/// <param name="Seats">The most seats the map is laid out for (the <c>@seats</c> directive).</param>
/// <param name="DeployZones">
/// Each seat's deploy zone (the <c>@deploy</c> directives), or <c>null</c> for the default: P1 on the left edge and
/// P2 on the right, <see cref="Rules.RulesConfig.DeployColumns"/> wide.
/// </param>
public sealed record GameMap(
    string Name,
    int Width,
    int Height,
    ImmutableArray<Terrain> Tiles,
    ImmutableArray<Point> Objectives,
    int Seats = 2,
    ImmutableSortedDictionary<Seat, ImmutableArray<TileRect>>? DeployZones = null)
{
    /// <summary>Whether <paramref name="seat"/> has a deploy zone on this map.</summary>
    public bool HasDeployZone(Seat seat) =>
        DeployZones?.ContainsKey(seat) ?? SeatExtensions.TwoPlayer.Contains(seat);

    /// <summary>The terrain at <paramref name="point"/>.</summary>
    public Terrain this[Point point] => Tiles[Index(point)];

    /// <summary>Whether <paramref name="point"/> lies on the map.</summary>
    public bool Contains(Point point) => point.X >= 0 && point.Y >= 0 && point.X < Width && point.Y < Height;

    /// <summary>Every tile on the map, row by row.</summary>
    public IEnumerable<Point> AllPoints() =>
        Enumerable.Range(0, Height).SelectMany(y => Enumerable.Range(0, Width).Select(x => new Point(x, y)));

    /// <summary>Returns a copy with the terrain at <paramref name="point"/> replaced.</summary>
    public GameMap WithTerrain(Point point, Terrain terrain) =>
        this with { Tiles = Tiles.SetItem(Index(point), terrain) };

    /// <summary>Whether <paramref name="point"/> is in <paramref name="seat"/>'s deploy zone (GameDesign §4.4).</summary>
    /// <param name="point">The tile.</param>
    /// <param name="seat">The deploying seat.</param>
    /// <param name="columns">
    /// Width of the default zones in columns, used when the map has no <c>@deploy</c> directives.
    /// </param>
    public bool IsInDeployZone(Point point, Seat seat, int columns) =>
        Contains(point) && (DeployZones is null
            ? seat switch
            {
                Seat.P1 => point.X < columns,
                Seat.P2 => point.X >= Width - columns,
                _ => false,
            }
            : DeployZones.TryGetValue(seat, out ImmutableArray<TileRect> zone)
                && zone.Any(rect => rect.Contains(point)));

    /// <summary>Whether <paramref name="point"/> is in, or orthogonally next to, <paramref name="seat"/>'s zone.</summary>
    public bool IsInOrNextToDeployZone(Point point, Seat seat, int columns) =>
        Contains(point)
        && (IsInDeployZone(point, seat, columns)
            || point.Neighbors().Any(neighbor => IsInDeployZone(neighbor, seat, columns)));

    /// <summary>Renders the map as rows of terrain characters.</summary>
    public IEnumerable<string> ToRows() =>
        Enumerable.Range(0, Height)
            .Select(y => new string(Enumerable.Range(0, Width)
                .Select(x => TerrainChars.ToChar(this[new Point(x, y)]))
                .ToArray()));

    private int Index(Point point) =>
        Contains(point)
            ? point.Y * Width + point.X
            : throw new ArgumentOutOfRangeException(nameof(point), point, "Point is off the map.");
}
