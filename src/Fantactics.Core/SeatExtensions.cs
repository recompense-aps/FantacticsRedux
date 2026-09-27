namespace Fantactics.Core;

/// <summary>Helpers for <see cref="Seat"/>.</summary>
public static class SeatExtensions
{
    /// <summary>All seats, in canonical order.</summary>
    public static readonly IReadOnlyList<Seat> All = [Seat.P1, Seat.P2];

    /// <summary>Returns the other seat.</summary>
    public static Seat Opponent(this Seat seat) => seat == Seat.P1 ? Seat.P2 : Seat.P1;
}
