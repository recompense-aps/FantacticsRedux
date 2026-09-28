using System.Collections.Immutable;

namespace Fantactics.Sim.Tournaments;

/// <summary>Aggregate results of a tournament (Simulation §7, layer 6).</summary>
/// <param name="Rules">Short hash of the rules config played.</param>
/// <param name="Games">Matches played.</param>
/// <param name="P1">P1's bot and race.</param>
/// <param name="P2">P2's bot and race.</param>
/// <param name="P1Wins">Matches P1 won.</param>
/// <param name="P2Wins">Matches P2 won.</param>
/// <param name="Draws">Drawn matches.</param>
/// <param name="AverageTurns">Mean turns per match.</param>
/// <param name="EndReasons">How matches ended.</param>
/// <param name="UnitStats">Totals per seat and unit type.</param>
public sealed record TournamentSummary(
    string Rules,
    int Games,
    string P1,
    string P2,
    int P1Wins,
    int P2Wins,
    int Draws,
    double AverageTurns,
    ImmutableArray<EndReasonCount> EndReasons,
    ImmutableArray<UnitTypeStats> UnitStats);
