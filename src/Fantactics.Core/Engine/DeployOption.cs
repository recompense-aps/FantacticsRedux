using System.Collections.Immutable;
using Fantactics.Core.Geometry;

namespace Fantactics.Core.Engine;

/// <summary>A reserve unit that can be deployed this turn.</summary>
/// <param name="UnitId">The reserve unit.</param>
/// <param name="Type">Unit type.</param>
/// <param name="Cost">Command it costs to deploy.</param>
/// <param name="Tiles">Legal arrival tiles.</param>
public sealed record DeployOption(int UnitId, string Type, int Cost, ImmutableArray<Point> Tiles);
