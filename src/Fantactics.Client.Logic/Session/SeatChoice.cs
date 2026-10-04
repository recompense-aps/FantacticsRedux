using Fantactics.Core;

namespace Fantactics.Client.Logic.Session;

/// <summary>An option in the load screen's seat list.</summary>
/// <param name="Label">What the option says, e.g. <c>Play as P2 (bot:captain@easy)</c>.</param>
/// <param name="Seat">The seat to show first, or <c>null</c> for whichever human seat is to move.</param>
public readonly record struct SeatChoice(string Label, Seat? Seat);
