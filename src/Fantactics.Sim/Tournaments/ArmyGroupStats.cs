namespace Fantactics.Sim.Tournaments;

/// <summary>
/// Results of the armies in one group across a tournament, e.g. every mono-race army or every Elves+Goblins army
/// (RacesAndUnits §2.4 balance target). Both seats' armies count.
/// </summary>
/// <param name="Group">The group: an army shape (mono, two-race, three+) or a race mix such as Elves+Goblins.</param>
/// <param name="Armies">Armies in the group.</param>
/// <param name="Wins">Games those armies won.</param>
/// <param name="Draws">Games those armies drew.</param>
public sealed record ArmyGroupStats(string Group, int Armies, int Wins, int Draws)
{
    /// <summary>Wins plus half the draws, per army.</summary>
    public double Score => Armies == 0 ? 0 : Math.Round((Wins + Draws / 2.0) / Armies, 3);
}
