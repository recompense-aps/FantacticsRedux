using System.Collections.Immutable;
using Fantactics.Core;
using Fantactics.Core.Engine;
using Fantactics.Core.Geometry;
using Fantactics.Core.State;

namespace Fantactics.Ai.Evaluation;

/// <summary>
/// Which tiles each enemy unit could strike next turn: every tile in attack range (and line of sight) of every
/// tile it can move to, or of where it stands. Computed once per decision and reused for every option scored.
/// </summary>
public sealed class ThreatMap
{
    private readonly ImmutableSortedDictionary<int, IReadOnlySet<Point>> _targets;

    private ThreatMap(ImmutableSortedDictionary<int, IReadOnlySet<Point>> targets)
    {
        _targets = targets;
    }

    /// <summary>An empty map, for bots that ignore threats.</summary>
    public static ThreatMap Empty { get; } = new(ImmutableSortedDictionary<int, IReadOnlySet<Point>>.Empty);

    /// <summary>Computes the threats of the units on the field that are enemies of <paramref name="seat"/>.</summary>
    public static ThreatMap ForEnemiesOf(GameState state, Seat seat) =>
        new(state.FieldUnits
            .Where(unit => state.AreEnemies(unit.Owner, seat))
            .ToImmutableSortedDictionary(unit => unit.Id, unit => ThreatRange.Strike(state, unit)));

    /// <summary>Whether unit <paramref name="attackerId"/> could strike <paramref name="tile"/> next turn.</summary>
    public bool Threatens(int attackerId, Point tile) =>
        _targets.TryGetValue(attackerId, out IReadOnlySet<Point>? tiles) && tiles.Contains(tile);
}
