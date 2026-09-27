namespace Fantactics.Sim.Tournaments;

/// <summary>Totals for one unit type on one seat across a tournament.</summary>
/// <param name="Seat">P1 or P2.</param>
/// <param name="Type">Unit type.</param>
/// <param name="Fielded">Units that appeared on the field (placed, arrived, or summoned).</param>
/// <param name="Damage">Damage dealt.</param>
/// <param name="Kills">Final blows dealt.</param>
/// <param name="Deaths">Units lost.</param>
public sealed record UnitTypeStats(string Seat, string Type, int Fielded, int Damage, int Kills, int Deaths);
