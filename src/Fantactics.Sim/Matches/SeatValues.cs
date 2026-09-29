using System.Collections.Immutable;
using Fantactics.Core;

namespace Fantactics.Sim.Matches;

/// <summary>Per-seat numbers for a match setup from command-line options, such as draft budgets.</summary>
public static class SeatValues
{
    /// <summary>
    /// Combines a value for both seats with per-seat overrides; <c>null</c> when none was given.
    /// </summary>
    /// <param name="both">Applies to both seats unless overridden.</param>
    /// <param name="p1">P1's value.</param>
    /// <param name="p2">P2's value.</param>
    /// <param name="name">The option's name, for the error message.</param>
    /// <exception cref="SimException">A value isn't positive.</exception>
    public static ImmutableSortedDictionary<Seat, int>? Combine(int? both, int? p1, int? p2, string name)
    {
        var bySeat = ImmutableSortedDictionary.CreateBuilder<Seat, int>();
        if ((p1 ?? both) is int first)
        {
            bySeat[Seat.P1] = first;
        }

        if ((p2 ?? both) is int second)
        {
            bySeat[Seat.P2] = second;
        }

        if (bySeat.Values.Any(value => value <= 0))
        {
            throw new SimException($"{name} must be positive.");
        }

        return bySeat.Count == 0 ? null : bySeat.ToImmutable();
    }
}
