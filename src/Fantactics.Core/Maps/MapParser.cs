using System.Collections.Immutable;
using Fantactics.Core.Geometry;

namespace Fantactics.Core.Maps;

/// <summary>
/// Parses the ASCII map format: one <see cref="TerrainChars"/> character per tile, one line per row, plus optional
/// directives:
/// <list type="bullet">
/// <item><c>@objectives x,y x,y …</c> lists objective tiles.</item>
/// <item><c>@seats N</c> gives the most seats the map is laid out for (default 2).</item>
/// <item><c>@deploy P1 x1,y1-x2,y2 …</c> gives a seat's deploy zone as one or more rectangles (corners included).
/// Without any, P1 and P2 deploy on the left and right edges.</item>
/// </list>
/// </summary>
public static class MapParser
{
    private const string ObjectivesDirective = "@objectives";
    private const string SeatsDirective = "@seats";
    private const string DeployDirective = "@deploy";
    private const int MinSeats = 2;

    /// <summary>Parses map text. Blank lines and lines starting with <c>//</c> are ignored.</summary>
    /// <param name="name">Map identifier.</param>
    /// <param name="text">Map text.</param>
    /// <exception cref="FormatException">Rows are ragged or contain unknown characters.</exception>
    public static GameMap Parse(string name, string text) => Parse(name, text.Split('\n'));

    /// <summary>Parses map rows given as separate strings.</summary>
    /// <exception cref="FormatException">
    /// Rows are ragged, contain unknown characters, or a directive is malformed or off the map.
    /// </exception>
    public static GameMap Parse(string name, IEnumerable<string> rows)
    {
        List<string> lines = rows
            .Select(row => row.Trim())
            .Where(row => row.Length > 0 && !row.StartsWith("//", StringComparison.Ordinal))
            .ToList();
        List<string> terrain = lines.Where(line => !line.StartsWith('@')).ToList();
        if (terrain.Count == 0)
        {
            throw new FormatException("Map has no rows.");
        }

        int width = terrain[0].Length;
        if (terrain.Any(line => line.Length != width))
        {
            throw new FormatException("Map rows must all have the same length.");
        }

        ImmutableArray<Terrain> tiles = terrain
            .SelectMany(line => line)
            .Select(TerrainChars.FromChar)
            .ToImmutableArray();
        ImmutableArray<Point> objectives = Arguments(lines, ObjectivesDirective)
            .SelectMany(arguments => arguments)
            .Select(ParsePoint)
            .ToImmutableArray();
        int seats = ParseSeats(lines);
        ImmutableSortedDictionary<Seat, ImmutableArray<TileRect>>? zones = ParseDeployZones(lines);

        GameMap map = new(name, width, terrain.Count, tiles, objectives, seats, zones);
        if (!objectives.All(map.Contains))
        {
            throw new FormatException("Objective tiles must be on the map.");
        }

        if (zones is not null)
        {
            if (zones.Keys.Any(seat => (int)seat >= seats))
            {
                throw new FormatException($"Deploy zones name a seat beyond @seats {seats}.");
            }

            if (zones.Values.SelectMany(rects => rects).Any(rect => !map.Contains(rect.Min) || !map.Contains(rect.Max)))
            {
                throw new FormatException("Deploy zones must be on the map.");
            }
        }

        return map;
    }

    /// <summary>The space-separated arguments of every line starting with <paramref name="directive"/>.</summary>
    private static IEnumerable<string[]> Arguments(List<string> lines, string directive) =>
        lines
            .Where(line => line.StartsWith(directive + " ", StringComparison.Ordinal) || line == directive)
            .Select(line => line[directive.Length..]
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static int ParseSeats(List<string> lines) =>
        Arguments(lines, SeatsDirective).ToList() switch
        {
            [] => MinSeats,
            [[string count]] when int.TryParse(count, out int seats)
                && seats >= MinSeats
                && seats <= Enum.GetValues<Seat>().Length => seats,
            _ => throw new FormatException(
                $"Write @seats once, with a number from {MinSeats} to {Enum.GetValues<Seat>().Length}."),
        };

    private static ImmutableSortedDictionary<Seat, ImmutableArray<TileRect>>? ParseDeployZones(List<string> lines)
    {
        List<string[]> directives = Arguments(lines, DeployDirective).ToList();
        if (directives.Count == 0)
        {
            return null;
        }

        var zones = ImmutableSortedDictionary.CreateBuilder<Seat, ImmutableArray<TileRect>>();
        foreach (string[] arguments in directives)
        {
            if (arguments is not [string seatText, _, ..] || !Enum.TryParse(seatText, out Seat seat))
            {
                throw new FormatException("Write @deploy <seat> x1,y1-x2,y2 …, e.g. @deploy P1 0,0-2,13.");
            }

            ImmutableArray<TileRect> rects = [.. arguments.Skip(1).Select(ParseRect)];
            zones[seat] = zones.TryGetValue(seat, out ImmutableArray<TileRect> earlier)
                ? earlier.AddRange(rects)
                : rects;
        }

        return zones.ToImmutable();
    }

    private static TileRect ParseRect(string text) => text.Split('-') switch
    {
        [string corner] => TileRect.Spanning(ParsePoint(corner), ParsePoint(corner)),
        [string from, string to] => TileRect.Spanning(ParsePoint(from), ParsePoint(to)),
        _ => throw new FormatException($"'{text}' is not a tile range like 0,0-2,13."),
    };

    private static Point ParsePoint(string text) =>
        text.Split(',') is [string x, string y] && int.TryParse(x, out int px) && int.TryParse(y, out int py)
            ? new Point(px, py)
            : throw new FormatException($"'{text}' is not a tile like 9,6.");
}
