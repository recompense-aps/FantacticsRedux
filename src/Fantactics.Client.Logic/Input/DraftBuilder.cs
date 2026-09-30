using Fantactics.Core.Commands;
using Fantactics.Core.Engine;

namespace Fantactics.Client.Logic.Input;

/// <summary>
/// Builds a draft from a seat's <see cref="DraftOptions"/> (GameDesign §4.4): units go into the starting army or the
/// reserve, within the budget, the starting cap, and one of each unique unit. Every change is checked the way the
/// engine checks the finished draft, so <see cref="Build"/> is legal whenever <see cref="Problems"/> is empty.
/// </summary>
public sealed class DraftBuilder
{
    private readonly Dictionary<string, DraftUnitOption> _byType;
    private readonly List<string> _starting = [];
    private readonly List<string> _reserve = [];

    /// <summary>Starts an empty draft.</summary>
    /// <param name="options">What the seat may draft.</param>
    public DraftBuilder(DraftOptions options)
    {
        Options = options;
        _byType = options.Units.ToDictionary(unit => unit.Type);
    }

    /// <summary>What the seat may draft.</summary>
    public DraftOptions Options { get; }

    /// <summary>The starting army's unit types, in the order they were added.</summary>
    public IReadOnlyList<string> Starting => _starting;

    /// <summary>The reserve's unit types, in the order they were added.</summary>
    public IReadOnlyList<string> Reserve => _reserve;

    /// <summary>The starting army's total Cost.</summary>
    public int StartingCost => _starting.Sum(CostOf);

    /// <summary>The whole draft's total Cost.</summary>
    public int TotalCost => StartingCost + _reserve.Sum(CostOf);

    /// <summary>Draft points left.</summary>
    public int Remaining => Options.Budget - TotalCost;

    /// <summary>Why the draft can't be submitted yet; empty when it can.</summary>
    public IReadOnlyList<string> Problems => _starting.Count == 0 ? ["The starting army needs at least one unit."] : [];

    /// <summary>How many units of <paramref name="type"/> are drafted, in both groups.</summary>
    public int Count(string type) => _starting.Count(t => t == type) + _reserve.Count(t => t == type);

    /// <summary>Why <paramref name="type"/> can't be added to the group, or <c>null</c> if it can.</summary>
    /// <param name="type">A unit type.</param>
    /// <param name="reserve">The reserve rather than the starting army.</param>
    public string? WhyNot(string type, bool reserve)
    {
        if (!_byType.TryGetValue(type, out DraftUnitOption? unit))
        {
            return $"{type} isn't in this draft.";
        }

        if (unit.Unique && Count(type) > 0)
        {
            return $"{type} is unique: one per army.";
        }

        if (unit.Cost > Remaining)
        {
            return $"{type} costs {unit.Cost}; {Remaining} points left.";
        }

        return !reserve && StartingCost + unit.Cost > Options.StartingCap
            ? $"{type} would put the starting army over its cap of {Options.StartingCap}."
            : null;
    }

    /// <summary>Adds a unit to the starting army or the reserve.</summary>
    /// <returns>Why it couldn't be added, or <c>null</c> if it was.</returns>
    public string? Add(string type, bool reserve)
    {
        if (WhyNot(type, reserve) is string problem)
        {
            return problem;
        }

        (reserve ? _reserve : _starting).Add(type);
        return null;
    }

    /// <summary>Removes one unit of <paramref name="type"/> from a group.</summary>
    /// <returns>Whether there was one to remove.</returns>
    public bool Remove(string type, bool reserve)
    {
        List<string> group = reserve ? _reserve : _starting;
        int index = group.LastIndexOf(type);
        if (index < 0)
        {
            return false;
        }

        group.RemoveAt(index);
        return true;
    }

    /// <summary>Moves one unit of <paramref name="type"/> to the other group.</summary>
    /// <param name="type">The unit type.</param>
    /// <param name="fromReserve">Whether it's in the reserve now (and goes to the starting army).</param>
    /// <returns>Why it couldn't move, or <c>null</c> if it did.</returns>
    public string? Move(string type, bool fromReserve)
    {
        if (!(fromReserve ? _reserve : _starting).Contains(type))
        {
            return $"No {type} to move.";
        }

        if (fromReserve && StartingCost + CostOf(type) > Options.StartingCap)
        {
            return $"{type} would put the starting army over its cap of {Options.StartingCap}.";
        }

        Remove(type, fromReserve);
        (fromReserve ? _starting : _reserve).Add(type);
        return null;
    }

    /// <summary>Removes every unit.</summary>
    public void Clear()
    {
        _starting.Clear();
        _reserve.Clear();
    }

    /// <summary>Replaces the draft with <paramref name="draft"/> (e.g. a bot's suggestion), keeping what fits.</summary>
    public void Load(SubmitDraft draft)
    {
        Clear();
        foreach (string type in draft.Starting)
        {
            Add(type, reserve: false);
        }

        foreach (string type in draft.Reserve)
        {
            Add(type, reserve: true);
        }
    }

    /// <summary>The draft as a command.</summary>
    public SubmitDraft Build() => new([.. _starting], [.. _reserve]);

    private int CostOf(string type) => _byType[type].Cost;
}
