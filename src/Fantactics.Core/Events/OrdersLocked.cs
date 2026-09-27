using Fantactics.Core.State;

namespace Fantactics.Core.Events;

/// <summary>A player locked in hidden orders (draft, placement, or moves). The orders themselves stay hidden.</summary>
/// <param name="Seat">The player.</param>
/// <param name="Phase">The phase the orders are for.</param>
public sealed record OrdersLocked(Seat Seat, Phase Phase) : GameEvent;
