using Fantactics.Client.Common;
using Fantactics.Core.Maps;
using Godot;

namespace Fantactics.Client.Match.Board;

/// <summary>
/// A flat-colored terrain <see cref="TileSet"/> built at runtime, until there's art. Each <see cref="Terrain"/> is
/// the atlas tile at (terrain index, 0), so swapping in a real tile set only means keeping that layout.
/// </summary>
public static class PlaceholderTiles
{
    /// <summary>The atlas source id terrain uses.</summary>
    public const int SourceId = 0;

    /// <summary>Builds the tile set.</summary>
    public static TileSet Create()
    {
        Terrain[] terrains = Enum.GetValues<Terrain>();
        int size = GodotConversions.TileSize;
        Image image = Image.CreateEmpty(size * terrains.Length, size, false, Image.Format.Rgba8);
        TileSetAtlasSource source = new() { TextureRegionSize = new Vector2I(size, size) };
        foreach (Terrain terrain in terrains)
        {
            int x = (int)terrain * size;
            image.FillRect(new Rect2I(x, 0, size, size), ColorOf(terrain).Darkened(0.15f));
            image.FillRect(new Rect2I(x + 1, 1, size - 2, size - 2), ColorOf(terrain));
        }

        source.Texture = ImageTexture.CreateFromImage(image);
        foreach (Terrain terrain in terrains)
        {
            source.CreateTile(AtlasCoords(terrain));
        }

        TileSet tileSet = new() { TileSize = new Vector2I(size, size) };
        tileSet.AddSource(source, SourceId);
        return tileSet;
    }

    /// <summary>Where <paramref name="terrain"/> sits in the atlas.</summary>
    public static Vector2I AtlasCoords(Terrain terrain) => new((int)terrain, 0);

    private static Color ColorOf(Terrain terrain) => terrain switch
    {
        Terrain.Plains => new Color("7fae5a"),
        Terrain.Road => new Color("b89c6c"),
        Terrain.Forest => new Color("2f6b35"),
        Terrain.Hills => new Color("a39659"),
        Terrain.Mountains => new Color("7c7672"),
        Terrain.Bridge => new Color("8a6a44"),
        Terrain.Water => new Color("3a6ea5"),
        _ => Colors.Magenta,
    };
}
