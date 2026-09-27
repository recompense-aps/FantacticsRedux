using System.Collections.Immutable;

namespace Fantactics.Sim.Views;

/// <summary>The options for the seat's pending decision (the <c>legal</c> command). Only the relevant parts are set.</summary>
/// <param name="Kind">Draft, Placement, Moves, or Action.</param>
/// <param name="Unit">The acting unit, for Action.</param>
/// <param name="Usage">How to submit this decision with <c>act</c>.</param>
/// <param name="Options">Numbered actions, for Action.</param>
/// <param name="Reach">Where each unit can move, for Moves.</param>
/// <param name="Deploys">Reserve units that can arrive, for Moves.</param>
/// <param name="Command">Command available, for Moves.</param>
/// <param name="MaxArrivals">Arrivals allowed this turn, for Moves.</param>
/// <param name="Draftable">Unit types and costs, for Draft.</param>
/// <param name="Budget">Draft budget, for Draft.</param>
/// <param name="StartingCap">Starting army cap, for Draft.</param>
/// <param name="ToPlace">Units to place, for Placement.</param>
/// <param name="PlaceTiles">Where they may go, for Placement.</param>
public sealed record LegalView(
    string Kind,
    string? Unit,
    string Usage,
    ImmutableArray<ActionRow>? Options = null,
    ImmutableArray<ReachRow>? Reach = null,
    ImmutableArray<DeployRow>? Deploys = null,
    int? Command = null,
    int? MaxArrivals = null,
    ImmutableArray<DraftRow>? Draftable = null,
    int? Budget = null,
    int? StartingCap = null,
    ImmutableArray<string>? ToPlace = null,
    string? PlaceTiles = null);
