using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Fantactics.Core.Engine;

/// <summary>Public totals for one player (shown in the HUD, GameDesign §4.5).</summary>
/// <param name="Seat">The player.</param>
/// <param name="Command">Unspent Command.</param>
/// <param name="DestroyedValue">Enemy value destroyed.</param>
/// <param name="ObjectivePoints">Points scored for holding objectives.</param>
/// <param name="ArmyValue">Value on the field plus reserve.</param>
/// <param name="ReserveValue">Value of the undeployed reserve (its composition stays hidden).</param>
/// <param name="OrdersLocked">Whether the player locked in hidden orders for the current phase.</param>
/// <param name="AllowedRaces">Races the player may draft from, or <c>null</c> for every race.</param>
/// <param name="DraftedRaces">
/// Units drafted per race, or <c>null</c> until both drafts are in (the draft reveal, GameDesign §4.4).
/// </param>
/// <param name="DraftBudget">The player's draft budget; Rout is measured against it (GameDesign §4.5).</param>
/// <param name="StartingCap">The most Cost the player may place at the start.</param>
/// <param name="Team">The player's team; enemies are players on other teams.</param>
/// <param name="Eliminated">Whether the player was routed out of a match that went on.</param>
public sealed record PlayerSummary(
    Seat Seat,
    int Command,
    int DestroyedValue,
    int ObjectivePoints,
    int ArmyValue,
    int ReserveValue,
    bool OrdersLocked,
    ImmutableSortedSet<string>? AllowedRaces,
    ImmutableSortedDictionary<string, int>? DraftedRaces,
    int DraftBudget,
    int StartingCap,
    int Team = 0,
    bool Eliminated = false)
{
    /// <summary>The player's team; a summary without one (0) is a team of its own.</summary>
    [JsonIgnore]
    public int TeamNumber => Team > 0 ? Team : Seat.OwnTeam();
}
