using System.Collections.Immutable;

namespace Fantactics.Core.Maps;

/// <summary>Parses the ASCII map format (one <see cref="TerrainChars"/> character per tile, one line per row).</summary>
public static class MapParser
{
    /// <summary>Parses map text. Blank lines and lines starting with <c>//</c> are ignored.</summary>
    /// <param name="name">Map identifier.</param>
    /// <param name="text">Map text.</param>
    /// <exception cref="FormatException">Rows are ragged or contain unknown characters.</exception>
    public static GameMap Parse(string name, string text) => Parse(name, text.Split('\n'));

    /// <summary>Parses map rows given as separate strings.</summary>
    /// <exception cref="FormatException">Rows are ragged or contain unknown characters.</exception>
    public static GameMap Parse(string name, IEnumerable<string> rows)
    {
        List<string> lines = rows
            .Select(row => row.Trim())
            .Where(row => row.Length > 0 && !row.StartsWith("//", StringComparison.Ordinal))
            .ToList();
        if (lines.Count == 0)
        {
            throw new FormatException("Map has no rows.");
        }

        int width = lines[0].Length;
        if (lines.Any(line => line.Length != width))
        {
            throw new FormatException("Map rows must all have the same length.");
        }

        ImmutableArray<Terrain> tiles = lines
            .SelectMany(line => line)
            .Select(TerrainChars.FromChar)
            .ToImmutableArray();
        return new GameMap(name, width, lines.Count, tiles);
    }
}
