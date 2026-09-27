using Fantactics.Core.Geometry;

namespace Fantactics.Core.Events;

/// <summary>A reserve unit was deployed.</summary>
/// <param name="UnitId">The unit.</param>
/// <param name="Owner">Owning seat.</param>
/// <param name="Type">Unit type.</param>
/// <param name="Tile">Arrival tile.</param>
/// <param name="CommandSpent">Command paid.</param>
/// <param name="CanAct">False if it arrived outside its deploy zone and follows the Summoned rule this turn.</param>
public sealed record UnitArrived(int UnitId, Seat Owner, string Type, Point Tile, int CommandSpent, bool CanAct)
    : GameEvent;
