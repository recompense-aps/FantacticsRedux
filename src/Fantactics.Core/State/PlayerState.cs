namespace Fantactics.Core.State;

/// <summary>Per-player totals.</summary>
/// <param name="Seat">The player's seat.</param>
/// <param name="Race">Race identifier.</param>
/// <param name="Command">Unspent Command (GameDesign §4.4).</param>
/// <param name="DestroyedValue">Cost of enemy units this player has destroyed (GameDesign §4.5).</param>
public sealed record PlayerState(Seat Seat, string Race, int Command, int DestroyedValue);
