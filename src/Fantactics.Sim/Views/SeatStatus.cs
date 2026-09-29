namespace Fantactics.Sim.Views;

/// <summary>One seat in <see cref="StatusView"/>.</summary>
/// <param name="Seat">P1 or P2.</param>
/// <param name="Player">llm, human, or bot:name.</param>
/// <param name="Races">Races the seat may draft: <c>any</c>, or a list.</param>
/// <param name="Budget">Draft budget and starting cap, e.g. <c>40/30</c>.</param>
/// <param name="Owes">The decision the seat owes (Draft, Placement, Moves, Action), or empty.</param>
public sealed record SeatStatus(string Seat, string Player, string Races, string Budget, string Owes);
