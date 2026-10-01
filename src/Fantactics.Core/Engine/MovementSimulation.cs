using System.Collections.Immutable;
using Fantactics.Core.Events;
using Fantactics.Core.Geometry;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Core.Engine;

/// <summary>
/// Steps every moving unit one tile per tick and applies zone of control, clashes, blocking, and friendly
/// pass-through (GameDesign §4.1). Mutable scratch state for a single resolution; the result is read back into an
/// immutable <see cref="GameState"/>.
/// </summary>
internal sealed class MovementSimulation
{
    private readonly GameState _state;
    private readonly List<GameEvent> _events;
    private readonly Dictionary<int, Point> _positions;
    private readonly Dictionary<int, Point> _starts;
    private readonly Dictionary<int, Queue<Point>> _remaining;
    private readonly Dictionary<int, List<Point>> _traveled;
    private readonly SortedSet<int> _active;
    private readonly Dictionary<int, int> _arrivedAtTick = [];
    private readonly HashSet<Point> _contested = [];
    private readonly List<PendingClash> _clashes = [];
    private readonly Dictionary<Seat, int> _teams;

    /// <summary>Prepares a simulation.</summary>
    /// <param name="state">State after arrivals, before movement.</param>
    /// <param name="paths">Validated paths by unit id; units without a path hold.</param>
    /// <param name="events">Receives movement events.</param>
    public MovementSimulation(GameState state, IReadOnlyDictionary<int, ImmutableArray<Point>> paths, List<GameEvent> events)
    {
        _state = state;
        _events = events;
        _positions = state.FieldUnits.ToDictionary(unit => unit.Id, unit => unit.Position);
        _starts = new Dictionary<int, Point>(_positions);
        _remaining = paths.ToDictionary(pair => pair.Key, pair => new Queue<Point>(pair.Value));
        _traveled = paths.ToDictionary(pair => pair.Key, pair => new List<Point> { _positions[pair.Key] });
        _active = new SortedSet<int>(paths.Keys);
        _teams = state.Seats.ToDictionary(seat => seat, state.TeamOf);
    }

    /// <summary>Final positions by unit id.</summary>
    public IReadOnlyDictionary<int, Point> Positions => _positions;

    /// <summary>Clashes to resolve, in the order they were marked.</summary>
    public IReadOnlyList<PendingClash> Clashes => _clashes;

    /// <summary>Units that ended on a different tile than they started.</summary>
    public IEnumerable<int> Moved => _positions.Where(pair => _starts[pair.Key] != pair.Value).Select(pair => pair.Key);

    /// <summary>Runs ticks until no unit is moving.</summary>
    public void Run()
    {
        for (int tick = 1; _active.Count > 0; tick++)
        {
            RunTick(tick);
        }
    }

    private void RunTick(int tick)
    {
        Dictionary<int, Point> intents = _active.ToDictionary(id => id, id => _remaining[id].Peek());
        SortedDictionary<int, StopReason> stops = [];

        foreach (int id in _active.Where(id => _contested.Contains(intents[id])))
        {
            stops[id] = StopReason.Blocked;
        }

        MarkSwaps(tick, intents, stops);

        // Stopping a unit can keep it on a tile others wanted, which can change both checks; repeat until stable.
        bool changed = true;
        while (changed)
        {
            changed = BlockEnemyOccupiedTiles(intents, stops);
            changed |= MarkSameTileContests(tick, intents, stops);
        }

        Dictionary<int, Point> before = new(_positions);
        List<int> movers = _active.Where(id => !stops.ContainsKey(id)).ToList();
        foreach (int id in movers)
        {
            _positions[id] = intents[id];
            _remaining[id].Dequeue();
            _traveled[id].Add(intents[id]);
            _arrivedAtTick[id] = tick;
            _events.Add(new UnitStepped(tick, id, before[id], intents[id]));
        }

        foreach ((int id, StopReason reason) in stops)
        {
            _active.Remove(id);
            _events.Add(new UnitStopped(tick, id, _positions[id], reason));
        }

        foreach (int id in movers)
        {
            if (_remaining[id].Count == 0)
            {
                _active.Remove(id);
                continue;
            }

            // Zone of control only triggers on becoming adjacent to an enemy it wasn't adjacent to before the tick.
            HashSet<int> adjacentBefore = AdjacentEnemies(id, before);
            if (AdjacentEnemies(id, _positions).Any(enemy => !adjacentBefore.Contains(enemy)))
            {
                _active.Remove(id);
                _events.Add(new UnitStopped(tick, id, _positions[id], StopReason.ZoneOfControl));
            }
        }

        SettleFriendlyOverlaps(tick);
    }

    private void MarkSwaps(int tick, Dictionary<int, Point> intents, SortedDictionary<int, StopReason> stops)
    {
        foreach (int id in _active)
        {
            if (stops.ContainsKey(id))
            {
                continue;
            }

            int? other = _positions
                .Where(pair => pair.Value == intents[id] && AreEnemies(pair.Key, id))
                .Select(pair => (int?)pair.Key)
                .FirstOrDefault();
            if (other is int enemy
                && !stops.ContainsKey(enemy)
                && intents.TryGetValue(enemy, out Point enemyIntent)
                && enemyIntent == _positions[id])
            {
                MarkContest(tick, id, enemy, null, stops);
            }
        }
    }

    /// <summary>
    /// Marks clashes for tiles units of two or more teams try to enter. Returns whether any unit was stopped.
    /// </summary>
    private bool MarkSameTileContests(int tick, Dictionary<int, Point> intents, SortedDictionary<int, StopReason> stops)
    {
        // A tile someone is staying on can't be contested: enemies of the occupant are blocked instead.
        List<IGrouping<Point, int>> groups = _active
            .Where(id => !stops.ContainsKey(id))
            .GroupBy(id => intents[id])
            .Where(group => !IsStayingOn(group.Key, intents, stops))
            .OrderBy(group => group.Key)
            .ToList();
        bool stopped = false;
        foreach (IGrouping<Point, int> group in groups)
        {
            List<List<int>> byTeam = group
                .GroupBy(Team)
                .OrderBy(teamGroup => teamGroup.Key)
                .Select(teamGroup => teamGroup
                    .OrderByDescending(BaseInitiative)
                    .ThenBy(id => id)
                    .ToList())
                .ToList();
            if (byTeam.Count < 2)
            {
                continue;
            }

            // Friendly collisions settle first: only each team's fastest unit contests the tile.
            foreach (int id in byTeam.SelectMany(ids => ids.Skip(1)))
            {
                stops[id] = StopReason.Blocked;
            }

            List<int> contenders = [.. byTeam.Select(ids => ids[0])];
            if (contenders.Count == 2)
            {
                MarkContest(tick, contenders[0], contenders[1], group.Key, stops);
            }
            else
            {
                MarkMultiWayContest(tick, contenders, group.Key, stops);
            }

            stopped = true;
        }

        return stopped;
    }

    private void MarkContest(int tick, int a, int b, Point? tile, SortedDictionary<int, StopReason> stops)
    {
        bool slipperyA = UnitRules.HasTrait(_state, _state.Units[a], TraitIds.Slippery);
        bool slipperyB = UnitRules.HasTrait(_state, _state.Units[b], TraitIds.Slippery);
        if (slipperyA || slipperyB)
        {
            if (slipperyA)
            {
                stops[a] = StopReason.Blocked;
                _events.Add(new ClashAvoided(tick, a, b));
            }

            if (slipperyB)
            {
                stops[b] = StopReason.Blocked;
                _events.Add(new ClashAvoided(tick, b, a));
            }

            return;
        }

        stops[a] = StopReason.Clash;
        stops[b] = StopReason.Clash;
        if (tile is Point contested)
        {
            _contested.Add(contested);
        }

        _clashes.Add(new PendingClash(tick, a, b, tile, []));
        _events.Add(new ClashMarked(tick, a, b, tile));
    }

    /// <summary>
    /// Three or more teams contest <paramref name="tile"/> (GameDesign §4.1). Slippery units step aside; if two or
    /// more others remain, they fight in turn, highest initiative first (ties in tie order), and the survivor of each
    /// fight takes on the next.
    /// </summary>
    private void MarkMultiWayContest(
        int tick,
        List<int> contenders,
        Point tile,
        SortedDictionary<int, StopReason> stops)
    {
        List<Seat> tieOrder = [.. _state.TieOrder];
        List<int> ordered = contenders
            .OrderByDescending(BaseInitiative)
            .ThenBy(id => tieOrder.IndexOf(Owner(id)))
            .ToList();
        List<int> fighters = ordered
            .Where(id => !UnitRules.HasTrait(_state, _state.Units[id], TraitIds.Slippery))
            .ToList();
        foreach (int slippery in ordered.Except(fighters))
        {
            stops[slippery] = StopReason.Blocked;
            int enemy = fighters.Count > 0 ? fighters[0] : ordered.First(id => id != slippery);
            _events.Add(new ClashAvoided(tick, slippery, enemy));
        }

        if (fighters.Count < 2)
        {
            // One fighter left walks onto the tile unopposed.
            return;
        }

        foreach (int id in fighters)
        {
            stops[id] = StopReason.Clash;
        }

        _contested.Add(tile);
        _clashes.Add(new PendingClash(tick, fighters[0], fighters[1], tile, [.. fighters.Skip(2)]));
        foreach (int challenger in fighters.Skip(1))
        {
            _events.Add(new ClashMarked(tick, fighters[0], challenger, tile));
        }
    }

    /// <summary>Stops units whose next tile holds an enemy that isn't leaving. Returns whether any unit was stopped.</summary>
    private bool BlockEnemyOccupiedTiles(Dictionary<int, Point> intents, SortedDictionary<int, StopReason> stops)
    {
        // Repeat until stable: a unit blocked this pass stays on its tile, which can block another unit behind it.
        bool any = false;
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (int id in _active.Where(id => !stops.ContainsKey(id)).ToList())
            {
                bool enemyStays = _positions.Any(pair =>
                    pair.Value == intents[id]
                    && AreEnemies(pair.Key, id)
                    && (!intents.ContainsKey(pair.Key) || stops.ContainsKey(pair.Key)));
                if (enemyStays)
                {
                    stops[id] = StopReason.Blocked;
                    changed = true;
                    any = true;
                }
            }
        }

        return any;
    }

    private bool IsStayingOn(Point tile, Dictionary<int, Point> intents, SortedDictionary<int, StopReason> stops) =>
        _positions.Any(pair => pair.Value == tile && (!intents.ContainsKey(pair.Key) || stops.ContainsKey(pair.Key)));

    private void SettleFriendlyOverlaps(int tick)
    {
        List<IGrouping<Point, int>> overlaps = _positions
            .GroupBy(pair => pair.Value, pair => pair.Key)
            .Where(group => group.Count(id => !_active.Contains(id)) > 1)
            .OrderBy(group => group.Key)
            .ToList();
        foreach (IGrouping<Point, int> group in overlaps)
        {
            // The unit that got there first keeps the tile; among same-tick arrivals, the higher initiative does.
            // If the others can't all back up that way, the next unit in that order keeps it instead.
            List<int> settled = group
                .Where(id => !_active.Contains(id))
                .OrderBy(id => _arrivedAtTick.GetValueOrDefault(id))
                .ThenByDescending(BaseInitiative)
                .ThenBy(id => id)
                .ToList();
            Dictionary<int, Point> plan = settled
                .Select(keeper => PlanBackUps(settled.Where(id => id != keeper).ToList()))
                .FirstOrDefault(candidate => candidate is not null)
                ?? [];
            foreach ((int id, Point backTo) in plan)
            {
                _positions[id] = backTo;
                _arrivedAtTick[id] = tick;
                _events.Add(new UnitStopped(tick, id, backTo, StopReason.BackedUp));
            }
        }
    }

    /// <summary>
    /// Plans a back-up for every unit in <paramref name="losers"/>, or returns <c>null</c> if any can't back up.
    /// </summary>
    private Dictionary<int, Point>? PlanBackUps(List<int> losers)
    {
        Dictionary<int, Point> plan = [];
        return losers.All(id => TryBackUp(id, plan, [.. losers])) ? plan : null;
    }

    /// <summary>
    /// Moves <paramref name="id"/> back along its path to the latest tile no stopped unit holds. Units still moving
    /// don't count as holding a tile: they leave next tick. If the tile is held by a friend that also moved this
    /// phase, that friend backs up too (recursively); a unit that never moved is never displaced.
    /// </summary>
    private bool TryBackUp(int id, Dictionary<int, Point> plan, HashSet<int> displacing)
    {
        if (!_traveled.TryGetValue(id, out List<Point>? path))
        {
            return false;
        }

        foreach (Point candidate in path.AsEnumerable().Reverse().Skip(1))
        {
            List<int> holders = _positions.Keys
                .Where(other => other != id
                    && !_active.Contains(other)
                    && plan.GetValueOrDefault(other, _positions[other]) == candidate)
                .ToList();
            if (holders.Count == 0)
            {
                plan[id] = candidate;
                return true;
            }

            if (holders.Any(other => displacing.Contains(other) || !_traveled.ContainsKey(other)))
            {
                continue;
            }

            Dictionary<int, Point> attempt = new(plan) { [id] = candidate };
            HashSet<int> chain = [.. displacing, .. holders];
            if (holders.All(other => TryBackUp(other, attempt, chain)))
            {
                foreach ((int unit, Point tile) in attempt)
                {
                    plan[unit] = tile;
                }

                return true;
            }
        }

        return false;
    }

    private HashSet<int> AdjacentEnemies(int id, IReadOnlyDictionary<int, Point> positions) =>
        positions
            .Where(pair => AreEnemies(pair.Key, id) && pair.Value.IsAdjacentTo(positions[id]))
            .Select(pair => pair.Key)
            .ToHashSet();

    private Seat Owner(int id) => _state.Units[id].Owner;

    private int Team(int id) => _teams[Owner(id)];

    private bool AreEnemies(int a, int b) => Team(a) != Team(b);

    private int BaseInitiative(int id) => _state.DefinitionOf(_state.Units[id]).Initiative;
}
