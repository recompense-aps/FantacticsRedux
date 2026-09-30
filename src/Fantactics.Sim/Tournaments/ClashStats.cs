namespace Fantactics.Sim.Tournaments;

/// <summary>
/// Clash results between two unit types across a tournament, from either seat. Each unordered pair has one row,
/// with <paramref name="TypeA"/> ordinally no later than <paramref name="TypeB"/>; a mirror (Archer vs Archer) is its
/// own row. In a mirror, A is whichever unit the clash event names first, so its rate says nothing about the type.
/// </summary>
/// <param name="TypeA">The first unit type.</param>
/// <param name="TypeB">The second unit type.</param>
/// <param name="Clashes">Clashes between the two types.</param>
/// <param name="AWins">Clashes a <paramref name="TypeA"/> unit won.</param>
/// <param name="BWins">Clashes a <paramref name="TypeB"/> unit won.</param>
/// <param name="Unresolved">Clashes stopped by the strike cap with both units alive.</param>
public sealed record ClashStats(string TypeA, string TypeB, int Clashes, int AWins, int BWins, int Unresolved)
{
    /// <summary><paramref name="TypeA"/>'s share of the decided clashes.</summary>
    public double AWinRate => AWins + BWins == 0 ? 0 : Math.Round((double)AWins / (AWins + BWins), 3);
}
