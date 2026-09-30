namespace Fantactics.Core;

/// <summary>Helpers for <see cref="Seat"/>.</summary>
public static class SeatExtensions
{
    /// <summary>The seats of a two-player match, the default setup.</summary>
    public static readonly IReadOnlyList<Seat> TwoPlayer = [Seat.P1, Seat.P2];

    /// <summary>The team a seat is on when the setup doesn't name one: its own, numbered from 1.</summary>
    public static int OwnTeam(this Seat seat) => (int)seat + 1;
}
