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
    private readonly Dictionary<Seat, int> _command = new() { [Seat.P1] = 0, [Seat.P2] = 0 };
    private readonly Dictionary<Seat, string> _races = new() { [Seat.P1] = "Elves", [Seat.P2] = "Goblins" };
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

    /// <summary>Sets both races.</summary>
    public ScenarioBuilder WithRaces(string p1, string p2)
    {
        _races[Seat.P1] = p1;
        _races[Seat.P2] = p2;
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

    /// <summary>Builds the state, in the movement phase of the configured turn.</summary>
    public GameState Build()
    {
        GameState state = GameEngine.NewMatch(_rules, _map, _races[Seat.P1], _races[Seat.P2], _seed);
        return state with
        {
            Turn = _turn,
            Phase = Phase.Movement,
            TiePriority = _tiePriority,
            Units = _units.ToImmutableSortedDictionary(unit => unit.Id, unit => unit),
            Players = state.Players.Values.ToImmutableSortedDictionary(
                player => player.Seat,
                player => player with { Command = _command[player.Seat] }),
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
