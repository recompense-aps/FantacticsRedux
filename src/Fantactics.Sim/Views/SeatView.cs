using System.Collections.Immutable;

namespace Fantactics.Sim.Views;

/// <summary>What one seat sees (the <c>view</c> command). Flat and uniform so it encodes compactly as TOON.</summary>
/// <param name="Turn">Current turn.</param>
/// <param name="Phase">Current phase.</param>
/// <param name="You">The viewing seat.</param>
/// <param name="TiePriority">Seat that wins initiative ties this turn.</param>
/// <param name="Me">The viewer's totals.</param>
/// <param name="Enemy">The opponent's public totals.</param>
/// <param name="Pending">What the viewer must decide now, if anything.</param>
/// <param name="WaitingFor">Seats that owe a decision.</param>
/// <param name="Legend">How to read the map and unit ids.</param>
/// <param name="Rows">The map, one string per row, with units drawn over terrain.</param>
/// <param name="Units">Visible units on the field.</param>
/// <param name="Reserve">The viewer's undeployed or unplaced units.</param>
/// <param name="Order">Action order still to come, as unit ids.</param>
/// <param name="Recent">Events since the viewer last acted.</param>
/// <param name="Outcome">Result, once the match is over.</param>
public sealed record SeatView(
    int Turn,
    string Phase,
    string You,
    string TiePriority,
    SideSummary Me,
    SideSummary Enemy,
    PendingInfo? Pending,
    ImmutableArray<string> WaitingFor,
    string Legend,
    ImmutableArray<string>? Rows,
    ImmutableArray<UnitRow> Units,
    ImmutableArray<ReserveRow> Reserve,
    string? Order,
    ImmutableArray<EventLine> Recent,
    string? Outcome);
