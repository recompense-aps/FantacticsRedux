using System.Collections.Immutable;
using Fantactics.Core.Commands;
using Fantactics.Core.Events;
using Fantactics.Core.State;

namespace Fantactics.Core.Engine;

/// <summary>
/// Per-player unit ids (decided 2026-09-28). Engine ids are assigned in draft order, so showing them would reveal how
/// many units the opponent drafted. Instead each seat numbers its own units 1, 2, 3, … in creation order and enemy
/// units <see cref="EnemyIdBase"/> + 1, + 2, … in the order they first appeared on the field. Everything a seat is
/// shown (views, legal options, events) uses these ids, and its commands are translated back with
/// <see cref="ToEngine(ICommand)"/>.
/// </summary>
public sealed class ViewIds
{
    /// <summary>Enemy unit ids start above this, so they never collide with the viewer's own.</summary>
    public const int EnemyIdBase = 1000;

    /// <summary>The id commands get for a unit the seat can't name; the engine rejects it as unknown.</summary>
    public const int Unknown = -1;

    private readonly ImmutableDictionary<int, int> _toView;
    private readonly ImmutableDictionary<int, int> _toEngine;

    private ViewIds(Seat seat, ImmutableDictionary<int, int> toView)
    {
        Seat = seat;
        _toView = toView;
        _toEngine = toView.ToImmutableDictionary(pair => pair.Value, pair => pair.Key);
    }

    /// <summary>The seat these ids are for.</summary>
    public Seat Seat { get; }

    /// <summary>The ids <paramref name="seat"/> sees in <paramref name="state"/>.</summary>
    /// <exception cref="InvalidOperationException">The seat has more units than <see cref="EnemyIdBase"/>.</exception>
    public static ViewIds For(GameState state, Seat seat)
    {
        List<int> own = state.Owners
            .Where(pair => pair.Value == seat)
            .Select(pair => pair.Key)
            .ToList();
        if (own.Count >= EnemyIdBase)
        {
            throw new InvalidOperationException($"{seat} has more than {EnemyIdBase - 1} units.");
        }

        IEnumerable<KeyValuePair<int, int>> ownIds = own
            .Select((id, index) => KeyValuePair.Create(id, index + 1));
        IEnumerable<KeyValuePair<int, int>> enemyIds = state.FieldOrder
            .Where(id => state.Owners.GetValueOrDefault(id) != seat)
            .Select((id, index) => KeyValuePair.Create(id, EnemyIdBase + index + 1));
        return new ViewIds(seat, ownIds.Concat(enemyIds).ToImmutableDictionary());
    }

    /// <summary>The seat's id for engine unit <paramref name="engineId"/>.</summary>
    /// <exception cref="InvalidOperationException">The seat can't see that unit (showing it would leak).</exception>
    public int ToView(int engineId) => _toView.TryGetValue(engineId, out int id)
        ? id
        : throw new InvalidOperationException($"Unit {engineId} is hidden from {Seat}.");

    /// <summary>The engine id for the seat's unit id, or <see cref="Unknown"/>.</summary>
    public int ToEngine(int viewId) => _toEngine.GetValueOrDefault(viewId, Unknown);

    /// <summary>A unit as the seat sees it.</summary>
    public Unit ToView(Unit unit) => unit with { Id = ToView(unit.Id) };

    /// <summary>Turn bookkeeping as the seat sees it.</summary>
    public TurnState ToView(TurnState turn) => new(
        [.. turn.Held.Select(ToView)],
        [.. turn.Braced.Select(ToView)],
        [.. turn.ClashWinners.Select(ToView)],
        turn.EffectiveInitiative.ToImmutableSortedDictionary(pair => ToView(pair.Key), pair => pair.Value),
        [.. turn.ActionQueue.Select(ToView)],
        [.. turn.DelayedQueue.Select(ToView)],
        [.. turn.HasDelayed.Select(ToView)],
        [.. turn.RetaliateUsed.Select(ToView)]);

    /// <summary>A pending decision as the seat sees it.</summary>
    public Decision ToView(Decision decision) => decision is ChooseUnitActionDecision choose
        ? choose with { UnitId = ToView(choose.UnitId) }
        : decision;

    /// <summary>Legal options with the seat's ids.</summary>
    public LegalActions ToView(LegalActions legal) => legal with
    {
        Decision = ToView(legal.Decision),
        Placement = legal.Placement is PlacementOptions placement
            ? placement with { UnitIds = [.. placement.UnitIds.Select(ToView)] }
            : null,
        Moves = legal.Moves is MoveOptions moves
            ? moves with
            {
                Units = [.. moves.Units.Select(unit => unit with { UnitId = ToView(unit.UnitId) })],
                Deploys = [.. moves.Deploys.Select(deploy => deploy with { UnitId = ToView(deploy.UnitId) })],
            }
            : null,
        Actions = [.. legal.Actions.Select(option => new ActionOption(
            ToView(option.Command),
            option.Preview is AttackPreview preview ? preview with { TargetId = ToView(preview.TargetId) } : null))],
    };

    /// <summary>An event as the seat sees it.</summary>
    public GameEvent ToView(GameEvent gameEvent) => gameEvent switch
    {
        AbilityUsed e => e with { UnitId = ToView(e.UnitId) },
        ClashAvoided e => e with { UnitId = ToView(e.UnitId), EnemyId = ToView(e.EnemyId) },
        ClashMarked e => e with { UnitA = ToView(e.UnitA), UnitB = ToView(e.UnitB) },
        ClashResolved e => e with
        {
            UnitA = ToView(e.UnitA),
            UnitB = ToView(e.UnitB),
            WinnerId = e.WinnerId is int winner ? ToView(winner) : null,
        },
        InitiativeOrdered e => e with { Order = [.. e.Order.Select(ToView)] },
        StatusApplied e => e with { UnitId = ToView(e.UnitId) },
        StatusRemoved e => e with { UnitId = ToView(e.UnitId) },
        UnitArrived e => e with { UnitId = ToView(e.UnitId) },
        UnitAttacked e => e with { AttackerId = ToView(e.AttackerId), TargetId = ToView(e.TargetId) },
        UnitDelayed e => e with { UnitId = ToView(e.UnitId) },
        UnitDied e => e with { UnitId = ToView(e.UnitId), KillerId = ToView(e.KillerId) },
        UnitHealed e => e with { UnitId = ToView(e.UnitId) },
        UnitPlaced e => e with { UnitId = ToView(e.UnitId) },
        UnitStepped e => e with { UnitId = ToView(e.UnitId) },
        UnitStopped e => e with { UnitId = ToView(e.UnitId) },
        UnitSummoned e => e with { UnitId = ToView(e.UnitId) },
        UnitWaited e => e with { UnitId = ToView(e.UnitId) },
        _ => gameEvent,
    };

    /// <summary>A command with the seat's ids, e.g. an option from <see cref="LegalActions"/>.</summary>
    public ICommand ToView(ICommand command) => Translate(command, ToView);

    /// <summary>A command the seat sent, with engine ids; ids it can't name become <see cref="Unknown"/>.</summary>
    public ICommand ToEngine(ICommand command) => Translate(command, ToEngine);

    private static ICommand Translate(ICommand command, Func<int, int> id) => command switch
    {
        PlaceStartingArmy c => c with
        {
            Placements = [.. c.Placements.Select(p => p with { UnitId = id(p.UnitId) })],
        },
        SubmitMoveOrders c => c with
        {
            Moves = [.. c.Moves.Select(move => move with { UnitId = id(move.UnitId) })],
            Deploys = [.. c.Deploys.Select(deploy => deploy with { UnitId = id(deploy.UnitId) })],
        },
        Attack c => c with { UnitId = id(c.UnitId), TargetId = id(c.TargetId) },
        UseAbility c => c with { UnitId = id(c.UnitId) },
        Wait c => c with { UnitId = id(c.UnitId) },
        Delay c => c with { UnitId = id(c.UnitId) },
        _ => command,
    };
}
