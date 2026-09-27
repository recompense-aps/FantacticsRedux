namespace Fantactics.Sim.Tournaments;

/// <summary>How many matches ended a certain way.</summary>
/// <param name="Reason">E.g. <c>Rout P2</c> or <c>TurnLimit draw</c>.</param>
/// <param name="Count">Number of matches.</param>
public sealed record EndReasonCount(string Reason, int Count);
