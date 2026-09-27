using System.Collections.Immutable;

namespace Fantactics.Sim.Views;

/// <summary>
/// What <c>act</c> prints on success: what happened, then the seat's next decision with its options, so a playing
/// seat needs one call per decision (Simulation §6.1).
/// </summary>
/// <param name="Result">Always <c>ok</c>.</param>
/// <param name="Events">Visible events since the command, including bot moves that followed.</param>
/// <param name="View">The seat's view, if it owes another decision.</param>
/// <param name="Legal">Options for that decision.</param>
/// <param name="WaitingFor">Seats that owe a decision, if not this one.</param>
/// <param name="Outcome">Result, once the match is over.</param>
public sealed record ActResult(
    string Result,
    ImmutableArray<EventLine> Events,
    SeatView? View,
    LegalView? Legal,
    ImmutableArray<string> WaitingFor,
    string? Outcome);
