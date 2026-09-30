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

    /// <summary>The match's seats, in seat order (GameDesign §3).</summary>
    [JsonIgnore]
    public IEnumerable<Seat> Seats => Players.Keys;

    /// <summary>Seats still playing: every seat that hasn't been eliminated, in seat order.</summary>
    [JsonIgnore]
    public IEnumerable<Seat> LiveSeats => Players.Values
        .Where(player => !player.Eliminated)
        .Select(player => player.Seat);

    /// <summary>
    /// Live seats in this turn's tie order: the seat with <see cref="TiePriority"/>, then the others in seat order,
    /// wrapping around (GameDesign §4.1).
    /// </summary>
    [JsonIgnore]
    public IEnumerable<Seat> TieOrder
    {
        get
        {
            List<Seat> live = LiveSeats.ToList();
            int first = Math.Max(0, live.IndexOf(TiePriority));
            return live.Skip(first).Concat(live.Take(first));
        }
    }

    /// <summary>Units on the map, by id.</summary>
    [JsonIgnore]
    public IEnumerable<Unit> FieldUnits => Units.Values.Where(unit => unit.IsOnField);

    /// <summary>The team <paramref name="seat"/> is on.</summary>
    public int TeamOf(Seat seat) => Players[seat].TeamNumber;

    /// <summary>Whether two seats are on different teams. A seat is never its own enemy.</summary>
    public bool AreEnemies(Seat a, Seat b) => a != b && TeamOf(a) != TeamOf(b);

    /// <summary>Whether the owners of two units are on different teams.</summary>
    public bool AreEnemies(Unit a, Unit b) => AreEnemies(a.Owner, b.Owner);

    /// <summary>Live seats on other teams than <paramref name="seat"/>, in seat order.</summary>
    public IEnumerable<Seat> Opponents(Seat seat) => LiveSeats.Where(other => AreEnemies(seat, other));

    /// <summary>
    /// The only opponent of <paramref name="seat"/>, for text and tools that only make sense one against one.
    /// </summary>
    /// <exception cref="InvalidOperationException">The seat has more than one opponent, or none.</exception>
    public Seat SoleOpponent(Seat seat) => Opponents(seat).ToList() is [Seat only]
        ? only
        : throw new InvalidOperationException($"{seat} doesn't have exactly one opponent.");

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
