using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Fantactics.Core.State;

namespace Fantactics.Core.Events;

/// <summary>The match ended.</summary>
/// <param name="Winners">The winning team's seats; empty for a draw.</param>
/// <param name="Reason">Why it ended.</param>
public sealed record MatchEnded(ImmutableArray<Seat> Winners, EndReason Reason) : GameEvent
{
    /// <summary>The winning team's seats; empty for a draw.</summary>
    public ImmutableArray<Seat> Winners { get; init; } = Winners.IsDefault ? [] : Winners;

    /// <summary>
    /// Reads the single winner of logs made before rules 0.7.0 as <see cref="Winners"/>. Never written.
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
}
