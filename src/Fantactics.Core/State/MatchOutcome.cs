using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Fantactics.Core.State;

/// <summary>The result of a finished match.</summary>
/// <param name="Winners">The winning team's seats; empty for a draw.</param>
/// <param name="Reason">Why the match ended.</param>
/// <param name="Placings">
/// Each seat's final place, 1 being best; teammates and seats that tied share a place. <c>null</c> in records made
/// before rules 0.7.0.
/// </param>
public sealed record MatchOutcome(
    ImmutableArray<Seat> Winners,
    EndReason Reason,
    ImmutableSortedDictionary<Seat, int>? Placings = null)
{
    /// <summary>The winning team's seats; empty for a draw.</summary>
    public ImmutableArray<Seat> Winners { get; init; } = Winners.IsDefault ? [] : Winners;

    /// <summary>
    /// Reads the single winner of records made before rules 0.7.0 as <see cref="Winners"/>. Never written.
    /// </summary>
    [JsonInclude]
    public Seat? Winner
    {
        private get => null;
        init
        {
            if (value is Seat winner)
            {
                Winners = [winner];
            }
        }
    }

    /// <summary>Whether nobody won.</summary>
    [JsonIgnore]
    public bool IsDraw => Winners.IsEmpty;

    /// <summary>Whether <paramref name="seat"/> is on the winning team.</summary>
    public bool Won(Seat seat) => Winners.Contains(seat);

    /// <summary>Who won, e.g. <c>P1 wins</c>, <c>P1 and P3 win</c>, or <c>Draw</c>.</summary>
    public string Headline() => Headline(Winners);

    /// <summary>Who won, e.g. <c>P1 wins</c>, <c>P1 and P3 win</c>, or <c>Draw</c>.</summary>
    /// <param name="winners">The winning team's seats; empty for a draw.</param>
    public static string Headline(ImmutableArray<Seat> winners) => winners.IsDefault ? "Draw" : winners switch
    {
        [] => "Draw",
        [Seat winner] => $"{winner} wins",
        _ => $"{string.Join(" and ", winners)} win",
    };
}
