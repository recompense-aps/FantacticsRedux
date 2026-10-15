using Fantactics.Client.Logic.Board;
using Fantactics.Core;
using Fantactics.Core.Engine;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Client.Logic.Log;

/// <summary>
/// Names for one seat's unit ids (its view ids), learned from its views and from units appearing. Names are never
/// forgotten, so a unit that died or left the view can still be named in the log.
/// </summary>
/// <param name="rules">Rules, for named characters' names.</param>
public sealed class UnitNames(RulesConfig rules)
{
    private readonly Dictionary<int, string> _names = [];

    /// <summary>
    /// A unit's short name with its owner, e.g. <c>P2 Grukk</c> or <c>P2 Wolf Rider</c>, or <c>unit 7</c> if it was
    /// never seen.
    /// </summary>
    public string this[int unitId] => _names.TryGetValue(unitId, out string? name) ? name : $"unit {unitId}";

    /// <summary>Learns every unit in <paramref name="view"/>.</summary>
    public void Learn(PlayerView view)
    {
        foreach (Unit unit in view.Units)
        {
            Learn(unit.Id, unit.Owner, unit.Type);
        }
    }

    /// <summary>Learns one unit's name.</summary>
    public void Learn(int unitId, Seat owner, string? type) =>
        _names[unitId] = $"{owner} {UnitText.Name(rules, type ?? "unit")}";
}
