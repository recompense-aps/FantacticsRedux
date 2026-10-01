using System.Collections.Immutable;
using Fantactics.Core;

namespace Fantactics.Sim.Matches;

/// <summary>Per-seat numbers for a match setup from command-line options, such as draft budgets.</summary>
public static class SeatValues
{
    /// <summary>
    /// Combines a value for both seats of a two-player match with per-seat overrides; <c>null</c> when none was given.
    /// </summary>
    /// <param name="both">Applies to both seats unless overridden.</param>
    /// <param name="p1">P1's value.</param>
    /// <param name="p2">P2's value.</param>
    /// <param name="name">The option's name, for the error message.</param>
    /// <exception cref="SimException">A value isn't positive.</exception>
    public static ImmutableSortedDictionary<Seat, int>? Combine(int? both, int? p1, int? p2, string name) =>
        Combine(both, new Dictionary<Seat, int?> { [Seat.P1] = p1, [Seat.P2] = p2 }, SeatExtensions.TwoPlayer, name);

    /// <summary>
    /// Combines a value for every seat with per-seat overrides; <c>null</c> when none was given.
    /// </summary>
    /// <param name="all">Applies to every seat unless overridden.</param>
    /// <param name="perSeat">
    /// Per-seat overrides; missing seats and <c>null</c> values use <paramref name="all"/>.
    /// </param>
    /// <param name="seats">The match's seats; values for other seats are ignored.</param>
    /// <param name="name">The option's name, for the error message.</param>
    /// <exception cref="SimException">A value isn't positive.</exception>
    public static ImmutableSortedDictionary<Seat, int>? Combine(
        int? all,
        IReadOnlyDictionary<Seat, int?> perSeat,
        IEnumerable<Seat> seats,
        string name)
    {
        var bySeat = ImmutableSortedDictionary.CreateBuilder<Seat, int>();
        foreach (Seat seat in seats)
        {
            if ((perSeat.GetValueOrDefault(seat) ?? all) is int value)
            {
                bySeat[seat] = value;
            }
        }

        if (bySeat.Values.Any(value => value <= 0))
        {
            throw new SimException($"{name} must be positive.");
        }

        return bySeat.Count == 0 ? null : bySeat.ToImmutable();
    }
}
