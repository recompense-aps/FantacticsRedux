namespace Fantactics.Sim.Views;

/// <summary>A player's public totals.</summary>
/// <param name="Seat">The seat.</param>
/// <param name="Race">Race.</param>
/// <param name="Command">Unspent Command.</param>
/// <param name="Army">Army value (field plus reserve).</param>
/// <param name="Reserve">Value of the undeployed reserve.</param>
/// <param name="Destroyed">Enemy value destroyed.</param>
/// <param name="Objective">Objective points; the turn-limit score is Destroyed + Objective.</param>
public sealed record SideSummary(string Seat, string Race, int Command, int Army, int Reserve, int Destroyed, int Objective);
