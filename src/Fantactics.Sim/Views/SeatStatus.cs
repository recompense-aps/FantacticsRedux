namespace Fantactics.Sim.Views;

/// <summary>One seat in <see cref="StatusView"/>.</summary>
/// <param name="Seat">P1 or P2.</param>
/// <param name="Player">llm, human, or bot:name.</param>
/// <param name="Race">Race.</param>
/// <param name="Owes">The decision the seat owes (Draft, Placement, Moves, Action), or empty.</param>
public sealed record SeatStatus(string Seat, string Player, string Race, string Owes);
