using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Fantactics.Core.Commands;
using Fantactics.Core.Geometry;
using Fantactics.Core.Maps;
using Fantactics.Core.Rules;

namespace Fantactics.Core.State;

/// <summary>The complete, immutable state of a match. Applying a command returns a new state.</summary>
/// <param name="Rules">Rules in effect; not part of the state hash (the config hash is recorded separately).</param>
/// <param name="Map">Current terrain (abilities can change it).</param>
/// <param name="Turn">Current turn, starting at 1; 0 before the first turn.</param>
/// <param name="Phase">Current phase.</param>
/// <param name="TiePriority">Seat that wins initiative ties this turn.</param>
/// <param name="Players">Per-player totals.</param>
/// <param name="Units">Every living unit by id, in any location.</param>
/// <param name="PendingOrders">Hidden orders already locked in for the current simultaneous phase.</param>
/// <param name="TurnState">Bookkeeping for the current turn.</param>
/// <param name="RngState">Seeded RNG state; all randomness in the rules comes from here.</param>
/// <param name="NextUnitId">Id the next created unit gets.</param>
/// <param name="Outcome">Result once the match is over.</param>
public sealed record GameState(
    [property: JsonIgnore] RulesConfig Rules,
    GameMap Map,
    int Turn,
    Phase Phase,
    Seat TiePriority,
    ImmutableSortedDictionary<Seat, PlayerState> Players,
    ImmutableSortedDictionary<int, Unit> Units,
    ImmutableSortedDictionary<Seat, ICommand> PendingOrders,
    TurnState TurnState,
    ulong RngState,
    int NextUnitId,
    MatchOutcome? Outcome)
{
    /// <summary>The owner of every unit ever created, including dead ones (for stable per-player ids).</summary>
    public ImmutableSortedDictionary<int, Seat> Owners { get; init; } = ImmutableSortedDictionary<int, Seat>.Empty;

    /// <summary>
    /// Every unit that has been on the field, in the order it first appeared. Players identify enemy units by this
    /// order (see <see cref="Engine.ViewIds"/>), so ids never reveal the size of the enemy's draft or reserve.
    /// </summary>
    public ImmutableArray<int> FieldOrder { get; init; } = [];

    /// <summary>Units on the map, by id.</summary>
    [JsonIgnore]
    public IEnumerable<Unit> FieldUnits => Units.Values.Where(unit => unit.IsOnField);

    /// <summary>The unit on <paramref name="tile"/>, if any.</summary>
    public Unit? UnitAt(Point tile) => FieldUnits.FirstOrDefault(unit => unit.Position == tile);

    /// <summary>The definition of <paramref name="unit"/>'s type.</summary>
    public UnitDefinition DefinitionOf(Unit unit) => Rules.Units[unit.Type];

    /// <summary>
    /// Returns a copy with <paramref name="unit"/> added or replaced, recording its owner and, the first time it's
    /// on the field, its place in <see cref="FieldOrder"/>.
    /// </summary>
    public GameState WithUnit(Unit unit) => this with
    {
        Units = Units.SetItem(unit.Id, unit),
        Owners = Owners.ContainsKey(unit.Id) ? Owners : Owners.Add(unit.Id, unit.Owner),
        FieldOrder = unit.IsOnField && !FieldOrder.Contains(unit.Id) ? FieldOrder.Add(unit.Id) : FieldOrder,
    };

    /// <summary>Returns a copy with <paramref name="player"/> replaced.</summary>
    public GameState WithPlayer(PlayerState player) => this with { Players = Players.SetItem(player.Seat, player) };
}
