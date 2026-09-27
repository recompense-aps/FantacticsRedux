using System.Text;

namespace Fantactics.Sim.Matches;

/// <summary>
/// Short, stable unit names from one seat's point of view (Simulation §6.1): uppercase letters for the viewer's
/// units, lowercase for enemy units in the order they were first seen. Enemy handles never reveal hidden units.
/// </summary>
public sealed class UnitHandles
{
    private readonly Dictionary<int, string> _byId = [];
    private readonly Dictionary<string, int> _byHandle = new(StringComparer.Ordinal);

    /// <summary>Builds the handles for a viewer.</summary>
    /// <param name="own">The viewer's unit ids, ever, in id order.</param>
    /// <param name="enemy">Enemy unit ids in the order the viewer first saw them.</param>
    public UnitHandles(IEnumerable<int> own, IEnumerable<int> enemy)
    {
        foreach ((int id, int rank) in own.Select((id, rank) => (id, rank)))
        {
            Add(id, Letters(rank));
        }

        foreach ((int id, int rank) in enemy.Select((id, rank) => (id, rank)))
        {
            Add(id, Letters(rank).ToLowerInvariant());
        }
    }

    /// <summary>The handle of a unit, or <c>#id</c> if the viewer has never seen it.</summary>
    public string Of(int unitId) => _byId.TryGetValue(unitId, out string? handle) ? handle : $"#{unitId}";

    /// <summary>The unit id for a handle (case-sensitive), if any.</summary>
    public int? Resolve(string handle) => _byHandle.TryGetValue(handle, out int id) ? id : null;

    /// <summary>A, B, …, Z, AA, AB, …</summary>
    private static string Letters(int rank)
    {
        StringBuilder letters = new();
        for (int n = rank + 1; n > 0; n = (n - 1) / 26)
        {
            letters.Insert(0, (char)('A' + (n - 1) % 26));
        }

        return letters.ToString();
    }

    private void Add(int id, string handle)
    {
        _byId[id] = handle;
        _byHandle[handle] = id;
    }
}
