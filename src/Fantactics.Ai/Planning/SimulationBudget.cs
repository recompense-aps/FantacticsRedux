using System.Diagnostics;
using Fantactics.Ai.Profiles;

namespace Fantactics.Ai.Planning;

/// <summary>Counts the simulations (and, if capped, the time) one decision has used.</summary>
public sealed class SimulationBudget
{
    private readonly ThinkBudget _budget;
    private readonly Stopwatch? _clock;

    /// <summary>Starts counting against <paramref name="budget"/>.</summary>
    public SimulationBudget(ThinkBudget budget)
    {
        _budget = budget;
        _clock = budget.MaxMilliseconds is null ? null : Stopwatch.StartNew();
    }

    /// <summary>Simulations used so far.</summary>
    public int Used { get; private set; }

    /// <summary>Claims one simulation; <c>false</c> once the budget is spent.</summary>
    public bool TryUse()
    {
        bool outOfTime = _clock is not null && _clock.ElapsedMilliseconds >= _budget.MaxMilliseconds;
        if (Used >= _budget.MaxSimulations || outOfTime)
        {
            return false;
        }

        Used++;
        return true;
    }
}
