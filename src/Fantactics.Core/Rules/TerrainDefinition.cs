namespace Fantactics.Core.Rules;

/// <summary>Rules for one terrain type (GameDesign §5).</summary>
/// <param name="MoveCost">Movement points to enter the tile, or <c>null</c> if impassable.</param>
/// <param name="Defense">Defense bonus (or penalty) for a unit standing on the tile.</param>
/// <param name="BlocksSight">Whether the tile blocks line of sight.</param>
public sealed record TerrainDefinition(int? MoveCost, int Defense, bool BlocksSight)
{
    /// <summary>Whether units can enter the tile at all.</summary>
    public bool IsPassable => MoveCost is not null;
}
