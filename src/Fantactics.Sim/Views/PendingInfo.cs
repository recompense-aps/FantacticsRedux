namespace Fantactics.Sim.Views;

/// <summary>The decision the viewer owes.</summary>
/// <param name="Kind">Draft, Placement, Moves, or Action.</param>
/// <param name="Unit">The acting unit's id, for Action.</param>
public sealed record PendingInfo(string Kind, string? Unit);
