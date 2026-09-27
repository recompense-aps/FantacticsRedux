using System.Collections.Immutable;

namespace Fantactics.Sim.Views;

/// <summary>Where the match is, with no hidden information (the <c>status</c> command).</summary>
/// <param name="Turn">Current turn.</param>
/// <param name="Phase">Current phase.</param>
/// <param name="Seats">Per-seat status.</param>
/// <param name="Commands">Commands in the record.</param>
/// <param name="Outcome">Result, once the match is over.</param>
public sealed record StatusView(int Turn, string Phase, ImmutableArray<SeatStatus> Seats, int Commands, string? Outcome);
