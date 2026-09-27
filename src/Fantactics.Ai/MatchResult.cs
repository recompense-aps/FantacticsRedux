using Fantactics.Core.Records;
using Fantactics.Core.State;

namespace Fantactics.Ai;

/// <summary>The result of a match played by <see cref="MatchRunner"/>.</summary>
/// <param name="Outcome">Winner and end reason.</param>
/// <param name="Turns">Turns played.</param>
/// <param name="FinalState">State at the end.</param>
/// <param name="Record">The full record, replayable with <see cref="MatchReplay"/>.</param>
public sealed record MatchResult(MatchOutcome Outcome, int Turns, GameState FinalState, MatchRecord Record);
