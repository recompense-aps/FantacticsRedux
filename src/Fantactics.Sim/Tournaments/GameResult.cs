namespace Fantactics.Sim.Tournaments;

/// <summary>One match of a tournament, as written to the CSV.</summary>
/// <param name="Game">Match index, from 0.</param>
/// <param name="Seed">Match seed.</param>
/// <param name="Winner">P1, P2, or draw.</param>
/// <param name="Reason">Rout or TurnLimit.</param>
/// <param name="Turns">Turns played.</param>
/// <param name="P1Army">P1's army value at the end.</param>
/// <param name="P2Army">P2's army value at the end.</param>
/// <param name="P1Destroyed">Value P1 destroyed.</param>
/// <param name="P2Destroyed">Value P2 destroyed.</param>
/// <param name="P1Objective">P1's objective points.</param>
/// <param name="P2Objective">P2's objective points.</param>
/// <param name="P1FirstContact">Turn P1 first dealt damage (attack or clash); 0 if never.</param>
/// <param name="P2FirstContact">Turn P2 first dealt damage; 0 if never.</param>
/// <param name="P1FirstArrival">Turn P1's first reserve arrived; 0 if never.</param>
/// <param name="P2FirstArrival">Turn P2's first reserve arrived; 0 if never.</param>
/// <param name="P1Damage">Damage P1's units dealt.</param>
/// <param name="P2Damage">Damage P2's units dealt.</param>
public sealed record GameResult(
    int Game,
    ulong Seed,
    string Winner,
    string Reason,
    int Turns,
    int P1Army,
    int P2Army,
    int P1Destroyed,
    int P2Destroyed,
    int P1Objective,
    int P2Objective,
    int P1FirstContact,
    int P2FirstContact,
    int P1FirstArrival,
    int P2FirstArrival,
    int P1Damage,
    int P2Damage)
{
    /// <summary>The CSV header matching <see cref="ToCsv"/>.</summary>
    public const string CsvHeader =
        "game,seed,winner,reason,turns,p1_army,p2_army,p1_destroyed,p2_destroyed,p1_objective,p2_objective,"
        + "p1_first_contact,p2_first_contact,p1_first_arrival,p2_first_arrival,p1_damage,p2_damage";

    /// <summary>One CSV line.</summary>
    public string ToCsv() =>
        $"{Game},{Seed},{Winner},{Reason},{Turns},{P1Army},{P2Army},{P1Destroyed},{P2Destroyed},"
        + $"{P1Objective},{P2Objective},{P1FirstContact},{P2FirstContact},{P1FirstArrival},{P2FirstArrival},"
        + $"{P1Damage},{P2Damage}";
}
