using Fantactics.Core;
using Godot;

namespace Fantactics.Client.Common;

/// <summary>Each seat's color, shared by unit tokens and the HUD's player list.</summary>
public static class SeatColors
{
    private static readonly Color _p1 = new("3b82f6");
    private static readonly Color _p2 = new("dc2626");
    private static readonly Color _p3 = new("16a34a");
    private static readonly Color _p4 = new("d97706");

    /// <summary>The color of <paramref name="seat"/>: blue, red, green, or amber.</summary>
    public static Color Of(Seat seat) => seat switch
    {
        Seat.P1 => _p1,
        Seat.P2 => _p2,
        Seat.P3 => _p3,
        _ => _p4,
    };
}
