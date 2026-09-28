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
    int P2Objective)
{
    /// <summary>The CSV header matching <see cref="ToCsv"/>.</summary>
    public const string CsvHeader = "game,seed,winner,reason,turns,p1_army,p2_army,p1_destroyed,p2_destroyed,p1_objective,p2_objective";

    /// <summary>One CSV line.</summary>
    public string ToCsv() =>
        $"{Game},{Seed},{Winner},{Reason},{Turns},{P1Army},{P2Army},{P1Destroyed},{P2Destroyed},{P1Objective},{P2Objective}";
}
