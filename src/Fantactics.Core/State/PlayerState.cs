using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Fantactics.Core.Rules;

namespace Fantactics.Core.State;

/// <summary>Per-player totals.</summary>
/// <param name="Seat">The player's seat.</param>
/// <param name="Command">Unspent Command (GameDesign §4.4).</param>
/// <param name="DestroyedValue">Cost of enemy units this player has destroyed (GameDesign §4.5).</param>
/// <param name="ObjectivePoints">Points scored for holding objectives (GameDesign §4.5).</param>
/// <param name="AllowedRaces">Races the player may draft from, or <c>null</c> for every race (GameDesign §4.4).</param>
/// <param name="DraftedRaces">
/// Units drafted per race, set once every draft is in; revealed to every other player before placement
/// (GameDesign §4.4).
/// </param>
/// <param name="DraftBudget">
/// The player's draft budget when the match setup overrides the rules' (GameDesign §4.4), or <c>null</c>.
/// </param>
/// <param name="StartingCap">The player's starting cap when the match setup overrides the rules', or <c>null</c>.</param>
/// <param name="Team">The player's team when the setup names one, or <c>null</c> for a team of its own.</param>
/// <param name="EliminatedOnTurn">
/// The turn the player was routed out of a match that went on without it (GameDesign §4.5), or <c>null</c>.
/// </param>
public sealed record PlayerState(
    Seat Seat,
    int Command,
    int DestroyedValue,
    int ObjectivePoints = 0,
    ImmutableSortedSet<string>? AllowedRaces = null,
    ImmutableSortedDictionary<string, int>? DraftedRaces = null,
    int? DraftBudget = null,
    int? StartingCap = null,
    int? Team = null,
    int? EliminatedOnTurn = null)
{
    /// <summary>Whether the player was eliminated: it has no units and no more decisions.</summary>
    [JsonIgnore]
    public bool Eliminated => EliminatedOnTurn is not null;

    /// <summary>The player's team: the setup's, else a team of its own. Enemies are players on other teams.</summary>
    [JsonIgnore]
    public int TeamNumber => Team ?? Seat.OwnTeam();

    /// <summary>Score compared at the turn limit: destroyed value plus objective points.</summary>
    public int Score => DestroyedValue + ObjectivePoints;

    /// <summary>Whether the player may draft units of <paramref name="race"/>.</summary>
    public bool MayDraft(string race) => AllowedRaces?.Contains(race) != false;

    /// <summary>The player's draft budget: the setup's override, else the rules'. Rout is measured against it.</summary>
    public int BudgetUnder(RulesConfig rules) => DraftBudget ?? rules.DraftBudget;

    /// <summary>The most Cost the player may place at the start: the setup's override, else the rules'.</summary>
    public int StartingCapUnder(RulesConfig rules) => StartingCap ?? rules.StartingCap;
}
