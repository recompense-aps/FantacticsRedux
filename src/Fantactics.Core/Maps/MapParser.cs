using System.Collections.Immutable;
using Fantactics.Core.Geometry;

namespace Fantactics.Core.Maps;

/// <summary>
/// Parses the ASCII map format: one <see cref="TerrainChars"/> character per tile, one line per row, plus an optional
/// <c>@objectives x,y x,y …</c> line listing objective tiles.
/// </summary>
public static class MapParser
{
    private const string ObjectivesDirective = "@objectives";

    /// <summary>Parses map text. Blank lines and lines starting with <c>//</c> are ignored.</summary>
    /// <param name="name">Map identifier.</param>
    /// <param name="text">Map text.</param>
    /// <exception cref="FormatException">Rows are ragged or contain unknown characters.</exception>
    public static GameMap Parse(string name, string text) => Parse(name, text.Split('\n'));

    /// <summary>Parses map rows given as separate strings.</summary>
    /// <exception cref="FormatException">Rows are ragged, contain unknown characters, or objectives are malformed.</exception>
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
        ImmutableArray<Point> objectives = lines
            .Where(line => line.StartsWith(ObjectivesDirective, StringComparison.Ordinal))
            .SelectMany(line => line[ObjectivesDirective.Length..]
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(ParsePoint)
            .ToImmutableArray();

        GameMap map = new(name, width, terrain.Count, tiles, objectives);
        return objectives.All(map.Contains)
            ? map
            : throw new FormatException("Objective tiles must be on the map.");
    }

    private static Point ParsePoint(string text) =>
        text.Split(',') is [string x, string y] && int.TryParse(x, out int px) && int.TryParse(y, out int py)
            ? new Point(px, py)
            : throw new FormatException($"'{text}' is not a tile like 9,6.");
}
