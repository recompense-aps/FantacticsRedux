using System.Collections.Immutable;

namespace Fantactics.Sim.Tournaments;

/// <summary>
/// Aggregate results of a tournament (Simulation §7, layer 6). Its size doesn't grow with the number of games, so
/// it stays cheap to read however large the run; per-game rows go to the CSV.
/// </summary>
/// <param name="Rules">Short hash of the rules config played.</param>
/// <param name="Games">Matches played.</param>
/// <param name="P1">P1's bot, and its allowed races if limited.</param>
/// <param name="P2">P2's bot, and its allowed races if limited.</param>
/// <param name="P1Wins">Matches P1 won.</param>
/// <param name="P2Wins">Matches P2 won.</param>
/// <param name="Draws">Drawn matches.</param>
/// <param name="P1Score">P1's score rate: wins plus half the draws, per game.</param>
/// <param name="P1ScoreLow">Lower end of the 95% (Wilson) confidence interval of <paramref name="P1Score"/>.</param>
/// <param name="P1ScoreHigh">Upper end of that interval.</param>
/// <param name="Significant">Whether the interval excludes 0.5, i.e. one side is really better.</param>
/// <param name="AverageTurns">Mean turns per match.</param>
/// <param name="EndReasons">How matches ended.</param>
/// <param name="Fingerprints">How each side played, per game.</param>
/// <param name="UnitStats">Totals per seat and unit type.</param>
/// <param name="ArmyShapes">Results by army shape: mono, two-race, or three+ races (RacesAndUnits §2.4).</param>
/// <param name="RaceMixes">Results by the exact races drafted, e.g. Elves+Goblins.</param>
public sealed record TournamentSummary(
    string Rules,
    int Games,
    string P1,
    string P2,
    int P1Wins,
    int P2Wins,
    int Draws,
    double P1Score,
    double P1ScoreLow,
    double P1ScoreHigh,
    bool Significant,
    double AverageTurns,
    ImmutableArray<EndReasonCount> EndReasons,
    ImmutableArray<StyleFingerprint> Fingerprints,
    ImmutableArray<UnitTypeStats> UnitStats,
    ImmutableArray<ArmyGroupStats> ArmyShapes,
    ImmutableArray<ArmyGroupStats> RaceMixes);
