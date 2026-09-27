using System.Collections.Immutable;

namespace Fantactics.Core.Engine;

/// <summary>Per-unit move options and reserve deploy options for the movement phase.</summary>
/// <param name="Units">Where each of the seat's units can move.</param>
/// <param name="Deploys">Affordable reserve units and their legal arrival tiles.</param>
/// <param name="Command">Command available to spend on deploys.</param>
/// <param name="MaxArrivals">Arrivals allowed this turn.</param>
public sealed record MoveOptions(
    ImmutableArray<UnitMoveOptions> Units,
    ImmutableArray<DeployOption> Deploys,
    int Command,
    int MaxArrivals);
