namespace Fantactics.Sim.Views;

/// <summary>A player's public totals.</summary>
/// <param name="Seat">The seat.</param>
/// <param name="Races">Units drafted per race, e.g. <c>Elves 5, Goblins 2</c>; empty until both drafts are in.</param>
/// <param name="Command">Unspent Command.</param>
/// <param name="Army">Army value (field plus reserve).</param>
/// <param name="Reserve">Value of the undeployed reserve.</param>
/// <param name="Destroyed">Enemy value destroyed.</param>
/// <param name="Objective">Objective points; the turn-limit score is Destroyed + Objective.</param>
public sealed record SideSummary(
    string Seat,
    string Races,
    int Command,
    int Army,
    int Reserve,
    int Destroyed,
    int Objective);
