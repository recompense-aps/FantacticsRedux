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
    public const string RulesVersion = "0.3.0";

    /// <summary>Creates a match waiting for both drafts.</summary>
    /// <param name="rules">Rules config.</param>
    /// <param name="map">Starting map.</param>
    /// <param name="p1Race">P1's race identifier.</param>
    /// <param name="p2Race">P2's race identifier.</param>
    /// <param name="seed">Seed for all rule randomness.</param>
    /// <exception cref="ArgumentException">A race is unknown.</exception>
    public static GameState NewMatch(RulesConfig rules, GameMap map, string p1Race, string p2Race, ulong seed)
    {
        foreach (string race in new[] { p1Race, p2Race }.Where(race => !rules.Races.ContainsKey(race)))
        {
            throw new ArgumentException($"Unknown race '{race}'.");
        }

        ImmutableSortedDictionary<Seat, PlayerState> players = new Dictionary<Seat, PlayerState>
        {
            [Seat.P1] = new(Seat.P1, p1Race, Command: 0, DestroyedValue: 0),
            [Seat.P2] = new(Seat.P2, p2Race, Command: 0, DestroyedValue: 0),
        }.ToImmutableSortedDictionary();

        return new GameState(
            rules,
            map,
            Turn: 0,
            Phase.Draft,
            TiePriority: Seat.P1,
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
        SeatExtensions.All
            .Where(seat => !state.PendingOrders.ContainsKey(seat))
            .Select(create)
            .ToImmutableArray();

    /// <summary>Stores a seat's hidden orders; once both seats are in, resolves the phase.</summary>
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
        if (state.PendingOrders.Count < SeatExtensions.All.Count)
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
