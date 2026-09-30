using System.Collections.Immutable;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Geometry;
using Fantactics.Core.Maps;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Core.Scenarios;

/// <summary>
/// Builds a mid-match state for focused tests and scenarios: a map, units already on the field, reserves, and
/// Command, starting in the movement phase (Simulation §2).
/// </summary>
/// <param name="rules">Rules config; defaults to <see cref="RulesConfig.Default"/>.</param>
public sealed class ScenarioBuilder(RulesConfig? rules = null)
{
    private readonly RulesConfig _rules = rules ?? RulesConfig.Default;
    private readonly List<Unit> _units = [];
    private readonly Dictionary<Seat, int> _command = [];
    private readonly Dictionary<Seat, ImmutableSortedSet<string>> _allowedRaces = [];
    private readonly Dictionary<Seat, int> _draftBudgets = [];
    private readonly Dictionary<Seat, int> _teams = [];
    private ImmutableSortedSet<Seat> _seats = [Seat.P1, Seat.P2];
    private GameMap _map = MapLibrary.Load("riverford");
    private int _turn = 1;
    private Seat _tiePriority = Seat.P1;
    private ulong _seed = 1;

    /// <summary>Uses a map given as ASCII rows (see <see cref="TerrainChars"/>).</summary>
    public ScenarioBuilder WithMap(params string[] rows)
    {
        _map = MapParser.Parse("scenario", rows);
        return this;
    }

    /// <summary>Marks objective tiles on the current map.</summary>
    public ScenarioBuilder WithObjectives(params (int X, int Y)[] tiles)
    {
        _map = _map with { Objectives = [.. tiles.Select(tile => new Point(tile.X, tile.Y))] };
        return this;
    }

    /// <summary>
    /// Plays with <paramref name="seats"/> (default P1 and P2). Deploy zones aren't checked, so any map works; give it
    /// <c>@deploy</c> lines to test arrivals.
    /// </summary>
    public ScenarioBuilder WithSeats(params Seat[] seats)
    {
        _seats = [.. seats];
        return this;
    }

    /// <summary>Puts <paramref name="seat"/> on <paramref name="team"/> (default: a team of its own).</summary>
    public ScenarioBuilder WithTeam(Seat seat, int team)
    {
        _teams[seat] = team;
        return this;
    }

    /// <summary>Limits the races <paramref name="seat"/> may draft from (default: every race).</summary>
    public ScenarioBuilder WithAllowedRaces(Seat seat, params string[] races)
    {
        _allowedRaces[seat] = [.. races];
        return this;
    }

    /// <summary>Overrides <paramref name="seat"/>'s draft budget, which its Rout threshold follows (GameDesign §4.5).</summary>
    public ScenarioBuilder WithDraftBudget(Seat seat, int budget)
    {
        _draftBudgets[seat] = budget;
        return this;
    }

    /// <summary>Sets the current turn (default 1).</summary>
    public ScenarioBuilder WithTurn(int turn)
    {
        _turn = turn;
        return this;
    }

    /// <summary>Sets which seat wins initiative ties this turn (default P1).</summary>
    public ScenarioBuilder WithTiePriority(Seat seat)
    {
        _tiePriority = seat;
        return this;
    }

    /// <summary>Sets a seat's unspent Command.</summary>
    public ScenarioBuilder WithCommand(Seat seat, int command)
    {
        _command[seat] = command;
        return this;
    }

    /// <summary>Sets the RNG seed (default 1).</summary>
    public ScenarioBuilder WithSeed(ulong seed)
    {
        _seed = seed;
        return this;
    }

    /// <summary>Places a unit on the field.</summary>
    /// <returns>The new unit's id, via <paramref name="id"/>.</returns>
    public ScenarioBuilder AddUnit(Seat seat, string type, int x, int y, out int id) =>
        Add(seat, type, UnitLocation.Field, new Point(x, y), out id);

    /// <summary>Places a unit on the field.</summary>
    public ScenarioBuilder AddUnit(Seat seat, string type, int x, int y) => AddUnit(seat, type, x, y, out _);

    /// <summary>Adds a unit to a seat's reserve.</summary>
    public ScenarioBuilder AddReserve(Seat seat, string type, out int id) =>
        Add(seat, type, UnitLocation.Reserve, default, out id);

    /// <summary>Replaces the last added unit, e.g. to set HP or statuses.</summary>
    public ScenarioBuilder Modify(Func<Unit, Unit> change)
    {
        _units[^1] = change(_units[^1]);
        return this;
    }

    /// <summary>
    /// Builds the state, in the movement phase of the configured turn. Each seat's drafted races are counted from
    /// its non-summoned units.
    /// </summary>
    public GameState Build()
    {
        GameState state = GameEngine.NewMatch(_rules, _map, _seed, _allowedRaces, _draftBudgets);
        return state with
        {
            Owners = _units.ToImmutableSortedDictionary(unit => unit.Id, unit => unit.Owner),
            FieldOrder = [.. _units.Where(unit => unit.IsOnField).Select(unit => unit.Id)],
            Turn = _turn,
            Phase = Phase.Movement,
            TiePriority = _tiePriority,
            Units = _units.ToImmutableSortedDictionary(unit => unit.Id, unit => unit),
            Players = _seats.ToImmutableSortedDictionary(
                seat => seat,
                seat => new PlayerState(
                    seat,
                    _command.GetValueOrDefault(seat),
                    DestroyedValue: 0,
                    AllowedRaces: _allowedRaces.GetValueOrDefault(seat),
                    DraftedRaces: DraftRules.CountRaces(_rules, _units
                        .Where(unit => unit.Owner == seat && !unit.IsSummoned)
                        .Select(unit => unit.Type)),
                    DraftBudget: _draftBudgets.TryGetValue(seat, out int budget) ? budget : null,
                    Team: _teams.TryGetValue(seat, out int team) ? team : null)),
            PendingOrders = ImmutableSortedDictionary<Seat, ICommand>.Empty,
            NextUnitId = _units.Count + 1,
        };
    }

    private ScenarioBuilder Add(Seat seat, string type, UnitLocation location, Point position, out int id)
    {
        id = _units.Count + 1;
        _units.Add(Unit.Create(id, seat, type, location, position, _rules.Units[type].Hp));
        return this;
    }
}
