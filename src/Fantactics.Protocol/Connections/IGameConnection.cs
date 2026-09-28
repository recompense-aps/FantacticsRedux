using Fantactics.Core;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Hosting;

namespace Fantactics.Protocol.Connections;

/// <summary>
/// One seat's link to a match, the only thing the client talks to (TechnicalDesign §2, decided 2026-09-28). The
/// local implementation drives an in-process <see cref="MatchHost"/>; a SignalR one will implement the same
/// interface later. Commands and updates use the seat's own unit ids (<see cref="ViewIds"/>).
/// </summary>
public interface IGameConnection
{
    /// <summary>Raised after every change the seat can see. May be raised on a background thread.</summary>
    event Action<SeatUpdate>? Updated;

    /// <summary>The seat this connection plays.</summary>
    Seat Seat { get; }

    /// <summary>The latest update: the view to draw and the seat's options, if it owes a decision.</summary>
    SeatUpdate Current { get; }

    /// <summary>Submits a command answering the seat's pending decision.</summary>
    /// <returns><c>null</c> if it was accepted, otherwise why not.</returns>
    Task<RuleViolation?> SubmitAsync(ICommand command);

    /// <summary>Queues an action for one of the seat's units that hasn't acted yet this turn.</summary>
    /// <returns><c>null</c> if it was queued, otherwise why not.</returns>
    Task<RuleViolation?> QueueAsync(IUnitActionCommand command);

    /// <summary>Turns auto-skip (units with nothing meaningful to do wait without asking) on or off.</summary>
    Task SetAutoSkipAsync(bool enabled);
}
