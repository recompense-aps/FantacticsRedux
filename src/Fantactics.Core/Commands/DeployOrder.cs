using Fantactics.Core.Geometry;

namespace Fantactics.Core.Commands;

/// <summary>Deploys a reserve unit with Command (GameDesign §4.4).</summary>
/// <param name="UnitId">The reserve unit.</param>
/// <param name="Tile">Arrival tile.</param>
public sealed record DeployOrder(int UnitId, Point Tile);
