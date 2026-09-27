namespace Fantactics.Core.Maps;

/// <summary>Maps that ship with the game, embedded in the Core assembly.</summary>
public static class MapLibrary
{
    private const string ResourcePrefix = "Fantactics.Core.Maps.Data.";
    private const string ResourceSuffix = ".txt";

    /// <summary>Names of all built-in maps.</summary>
    public static IReadOnlyList<string> Names { get; } = typeof(MapLibrary).Assembly
        .GetManifestResourceNames()
        .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
        .Select(name => name[ResourcePrefix.Length..^ResourceSuffix.Length])
        .Order(StringComparer.Ordinal)
        .ToList();

    /// <summary>Loads a built-in map by name.</summary>
    /// <exception cref="ArgumentException">No map has that name.</exception>
    public static GameMap Load(string name)
    {
        using Stream stream = typeof(MapLibrary).Assembly
            .GetManifestResourceStream(ResourcePrefix + name + ResourceSuffix)
            ?? throw new ArgumentException($"Unknown map '{name}'.", nameof(name));
        using StreamReader reader = new(stream);
        return MapParser.Parse(name, reader.ReadToEnd());
    }
}
