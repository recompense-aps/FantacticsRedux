using Fantactics.Core.Engine;
using Fantactics.Core.Geometry;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Client.Logic.Board;

/// <summary>
/// Where enemy units could move and attack next turn, computed from the seat's view alone so it never reveals
/// hidden information (enemy units on the field, their statuses, and the terrain are all public).
/// </summary>
/// <param name="Move">Tiles a shown enemy can move to.</param>
/// <param name="Attack">Tiles a shown enemy could attack but not move to.</param>
public sealed record ThreatOverlay(IReadOnlySet<Point> Move, IReadOnlySet<Point> Attack)
{
    /// <summary>No threats shown.</summary>
    public static ThreatOverlay None { get; } = new(new HashSet<Point>(), new HashSet<Point>());

    /// <summary>The threats of the enemy field units among <paramref name="unitIds"/>.</summary>
    /// <param name="view">The seat's view.</param>
    /// <param name="rules">Rules, for movement and attack ranges.</param>
    /// <param name="unitIds">Units to show; ids that aren't enemies on the field are ignored.</param>
    public static ThreatOverlay For(PlayerView view, RulesConfig rules, IEnumerable<int> unitIds)
    {
        HashSet<int> shown = [.. unitIds];
        List<Unit> enemies = view.Units
            .Where(unit => unit.IsOnField && shown.Contains(unit.Id) && view.AreEnemies(unit.Owner, view.Seat))
            .ToList();
        if (enemies.Count == 0)
        {
            return None;
        }

        GameState state = view.ToState(rules);
        HashSet<Point> move = enemies
            .SelectMany(unit => ThreatRange.Reach(state, unit))
            .ToHashSet();
        HashSet<Point> attack = enemies
            .SelectMany(unit => ThreatRange.Strike(state, unit))
            .Where(tile => !move.Contains(tile))
            .ToHashSet();
        return new ThreatOverlay(move, attack);
    }
}
