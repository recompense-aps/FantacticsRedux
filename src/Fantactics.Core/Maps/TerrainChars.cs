namespace Fantactics.Core.Maps;

/// <summary>The one-character terrain encoding used by map files, scenarios, and text views.</summary>
public static class TerrainChars
{
    private static readonly IReadOnlyDictionary<char, Terrain> _byChar = new Dictionary<char, Terrain>
    {
        ['.'] = Terrain.Plains,
        ['='] = Terrain.Road,
        ['%'] = Terrain.Forest,
        ['+'] = Terrain.Hills,
        ['^'] = Terrain.Mountains,
        ['#'] = Terrain.Bridge,
        ['~'] = Terrain.Water,
    };

    private static readonly IReadOnlyDictionary<Terrain, char> _byTerrain =
        _byChar.ToDictionary(pair => pair.Value, pair => pair.Key);

    /// <summary>Returns the character for <paramref name="terrain"/>.</summary>
    public static char ToChar(Terrain terrain) => _byTerrain[terrain];

    /// <summary>Parses a terrain character.</summary>
    /// <exception cref="FormatException">The character is not a terrain character.</exception>
    public static Terrain FromChar(char c) =>
        _byChar.TryGetValue(c, out Terrain terrain)
            ? terrain
            : throw new FormatException($"'{c}' is not a terrain character.");
}
