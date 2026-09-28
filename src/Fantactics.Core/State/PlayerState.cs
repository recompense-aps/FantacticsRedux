namespace Fantactics.Core.State;

/// <summary>Per-player totals.</summary>
/// <param name="Seat">The player's seat.</param>
/// <param name="Race">Race identifier.</param>
/// <param name="Command">Unspent Command (GameDesign §4.4).</param>
/// <param name="DestroyedValue">Cost of enemy units this player has destroyed (GameDesign §4.5).</param>
/// <param name="ObjectivePoints">Points scored for holding objectives (GameDesign §4.5).</param>
public sealed record PlayerState(Seat Seat, string Race, int Command, int DestroyedValue, int ObjectivePoints = 0)
{
    /// <summary>Score compared at the turn limit: destroyed value plus objective points.</summary>
    public int Score => DestroyedValue + ObjectivePoints;
}
