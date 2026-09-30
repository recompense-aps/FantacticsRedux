using System.Collections.Immutable;
using Fantactics.Core.Commands;
using Fantactics.Core.Events;
using Fantactics.Core.Maps;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Core.Engine;

/// <summary>
/// The rules engine every driver uses: the server, the client, bots, and simulations (Simulation §2).
/// All functions are pure: they take a state and return a new one.
/// </summary>
public static class GameEngine
{
    /// <summary>Version of the rules code, recorded in match records. Bump it when rule behavior changes.</summary>
    public const string RulesVersion = "0.6.0";

    /// <summary>Creates a one-against-one match (P1 and P2) waiting for both drafts.</summary>
    /// <param name="rules">Rules config.</param>
    /// <param name="map">Starting map.</param>
    /// <param name="seed">Seed for all rule randomness.</param>
    /// <param name="allowedRaces">
    /// Races each seat may draft from; a seat that's missing, or a <c>null</c> map, may draft every race.
    /// </param>
    /// <param name="draftBudgets">Per-seat draft budgets overriding the rules' (GameDesign §4.4); missing seats use it.</param>
    /// <param name="startingCaps">Per-seat starting caps overriding the rules'; missing seats use it.</param>
    /// <exception cref="ArgumentException">
    /// A race is unknown, a seat is allowed no races, or a budget or cap isn't positive.
    /// </exception>
    public static GameState NewMatch(
        RulesConfig rules,
        GameMap map,
        ulong seed,
        IReadOnlyDictionary<Seat, ImmutableSortedSet<string>>? allowedRaces = null,
        IReadOnlyDictionary<Seat, int>? draftBudgets = null,
        IReadOnlyDictionary<Seat, int>? startingCaps = null) =>
        NewMatch(rules, map, seed, SeatExtensions.TwoPlayer, null, allowedRaces, draftBudgets, startingCaps);

    /// <summary>Creates a match for <paramref name="seats"/>, waiting for every draft (GameDesign §3).</summary>
    /// <param name="rules">Rules config.</param>
    /// <param name="map">Starting map; it must have a deploy zone for every seat.</param>
    /// <param name="seed">Seed for all rule randomness.</param>
    /// <param name="seats">Two to <see cref="GameMap.Seats"/> distinct seats.</param>
    /// <param name="teams">
    /// Each seat's team; a missing seat, or <c>null</c>, is a team of its own. At least two teams must play.
    /// </param>
    /// <param name="allowedRaces">
    /// Races each seat may draft from; a seat that's missing, or a <c>null</c> map, may draft every race.
    /// </param>
    /// <param name="draftBudgets">Per-seat draft budgets overriding the rules' (GameDesign §4.4); missing seats use it.</param>
    /// <param name="startingCaps">Per-seat starting caps overriding the rules'; missing seats use it.</param>
    /// <exception cref="ArgumentException">
    /// The seats don't fit the map, fewer than two teams play, a race is unknown, a seat is allowed no races, or a
    /// budget or cap isn't positive.
    /// </exception>
    public static GameState NewMatch(
        RulesConfig rules,
        GameMap map,
        ulong seed,
        IReadOnlyCollection<Seat> seats,
        IReadOnlyDictionary<Seat, int>? teams,
        IReadOnlyDictionary<Seat, ImmutableSortedSet<string>>? allowedRaces = null,
        IReadOnlyDictionary<Seat, int>? draftBudgets = null,
        IReadOnlyDictionary<Seat, int>? startingCaps = null)
    {
        if (seats.Count < 2 || seats.Count > map.Seats || seats.Distinct().Count() != seats.Count)
        {
            throw new ArgumentException($"Map '{map.Name}' takes 2 to {map.Seats} distinct seats (got {seats.Count}).");
        }

        foreach (Seat seat in seats.Where(seat => !map.HasDeployZone(seat)))
        {
            throw new ArgumentException($"Map '{map.Name}' has no deploy zone for {seat}.");
        }

        if (seats.Select(seat => teams?.GetValueOrDefault(seat) is int team and > 0 ? team : seat.OwnTeam())
                .Distinct()
                .Count() < 2)
        {
            throw new ArgumentException("At least two teams must play.");
        }

        foreach (int team in teams?.Values ?? [])
        {
            if (team <= 0)
            {
                throw new ArgumentException($"Teams are numbered from 1 (got {team}).");
            }
        }

        foreach (int points in (draftBudgets?.Values ?? []).Concat(startingCaps?.Values ?? []))
        {
            if (points <= 0)
            {
                throw new ArgumentException($"Draft budgets and starting caps must be positive (got {points}).");
            }
        }

        foreach (ImmutableSortedSet<string> races in allowedRaces?.Values ?? [])
        {
            if (races.IsEmpty)
            {
                throw new ArgumentException("A seat must be allowed at least one race.");
            }

            foreach (string race in races.Where(race => !rules.Races.ContainsKey(race)))
            {
                throw new ArgumentException($"Unknown race '{race}'.");
            }
        }

        ImmutableSortedDictionary<Seat, PlayerState> players = seats.ToImmutableSortedDictionary(
            seat => seat,
            seat => new PlayerState(
                seat,
                Command: 0,
                DestroyedValue: 0,
                AllowedRaces: allowedRaces?.GetValueOrDefault(seat),
                DraftBudget: draftBudgets?.TryGetValue(seat, out int budget) == true ? budget : null,
                StartingCap: startingCaps?.TryGetValue(seat, out int cap) == true ? cap : null,
                Team: teams?.TryGetValue(seat, out int team) == true ? team : null));

        return new GameState(
            rules,
            map,
            Turn: 0,
            Phase.Draft,
            TiePriority: players.Keys.First(),
            players,
            Units: ImmutableSortedDictionary<int, Unit>.Empty,
            PendingOrders: ImmutableSortedDictionary<Seat, ICommand>.Empty,
            TurnState.Empty,
            RngState: seed,
            NextUnitId: 1,
            Outcome: null);
    }

    /// <summary>What the game is waiting for, per seat. Empty once the match is over.</summary>
    public static ImmutableArray<Decision> PendingDecisions(GameState state) => state.Phase switch
    {
        Phase.Draft => HiddenDecisions(state, seat => new DraftArmyDecision(seat)),
        Phase.Placement => HiddenDecisions(state, seat => new PlaceStartingArmyDecision(seat)),
        Phase.Movement => HiddenDecisions(state, seat => new SubmitMoveOrdersDecision(seat)),
        Phase.Action when state.TurnState.CurrentActor is int unitId =>
            [new ChooseUnitActionDecision(state.Units[unitId].Owner, unitId)],
        _ => [],
    };

    /// <summary>The decision <paramref name="seat"/> owes right now, if any.</summary>
    public static Decision? PendingDecisionFor(GameState state, Seat seat) =>
        PendingDecisions(state).FirstOrDefault(decision => decision.Seat == seat);

    /// <summary>Validates <paramref name="command"/> from <paramref name="seat"/> and applies it.</summary>
    /// <returns><see cref="Accepted"/> with the new state and events, or <see cref="Rejected"/> with the reason.</returns>
    public static ApplyResult Apply(GameState state, Seat seat, ICommand command)
    {
        List<GameEvent> events = [];
        try
        {
            GameState next = command switch
            {
                SubmitDraft draft => SubmitHidden(state, seat, draft, Phase.Draft, events),
                PlaceStartingArmy placement => SubmitHidden(state, seat, placement, Phase.Placement, events),
                SubmitMoveOrders orders => SubmitHidden(state, seat, orders, Phase.Movement, events),
                IUnitActionCommand action => ActionRules.Apply(state, seat, action, events),
                _ => throw new RuleViolationException("unknown-command", $"Unknown command {command.GetType().Name}."),
            };
            return new Accepted(next, [.. events]);
        }
        catch (RuleViolationException ex)
        {
            return new Rejected(ex.Violation);
        }
    }

    private static ImmutableArray<Decision> HiddenDecisions(GameState state, Func<Seat, Decision> create) =>
        state.LiveSeats
            .Where(seat => !state.PendingOrders.ContainsKey(seat))
            .Select(create)
            .ToImmutableArray();

    /// <summary>Stores a seat's hidden orders; once every live seat's are in, resolves the phase.</summary>
    private static GameState SubmitHidden(GameState state, Seat seat, ICommand command, Phase phase, List<GameEvent> events)
    {
        RuleViolationException.ThrowUnless(
            state.Phase == phase && !state.PendingOrders.ContainsKey(seat),
            "not-your-decision",
            $"{seat} can't submit {command.GetType().Name} now (phase {state.Phase}).");

        switch (command)
        {
            case SubmitDraft draft:
                DraftRules.Validate(state, seat, draft);
                break;
            case PlaceStartingArmy placement:
                PlacementRules.Validate(state, seat, placement);
                break;
            case SubmitMoveOrders orders:
                MoveOrderRules.Validate(state, seat, orders);
                break;
        }

        state = state with { PendingOrders = state.PendingOrders.SetItem(seat, command) };
        events.Add(new OrdersLocked(seat, phase));
        if (state.LiveSeats.Any(live => !state.PendingOrders.ContainsKey(live)))
        {
            return state;
        }

        return phase switch
        {
            Phase.Draft => DraftRules.Resolve(state),
            Phase.Placement => PlacementRules.Resolve(state, events),
            _ => MovementResolver.Resolve(state, events),
        };
    }
}
