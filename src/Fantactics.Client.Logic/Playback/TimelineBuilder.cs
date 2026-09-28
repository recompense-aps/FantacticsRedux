using System.Collections.Immutable;
using Fantactics.Core.Events;

namespace Fantactics.Client.Logic.Playback;

/// <summary>
/// Turns an update's events into beats to animate (TechnicalDesign §2.5). Consecutive steps of one movement tick
/// play together, as do consecutive units appearing; everything else gets its own beat. Events with nothing to
/// show are listed in <see cref="Ignored"/>. The board snaps to the update's view afterwards, so playback only has
/// to look right, never to be the source of truth.
/// </summary>
public static class TimelineBuilder
{
    /// <summary>Event types that have no animation (their effect shows in the view, panels, or log).</summary>
    public static ImmutableHashSet<Type> Ignored { get; } =
    [
        typeof(ClashAvoided),
        typeof(ClashResolved),
        typeof(CommandGained),
        typeof(InitiativeOrdered),
        typeof(ObjectiveScored),
        typeof(OrdersLocked),
        typeof(TurnEnded),
        typeof(UnitDelayed),
        typeof(UnitStopped),
        typeof(UnitWaited),
    ];

    /// <summary>The beats for <paramref name="events"/>, in order.</summary>
    public static ImmutableArray<Beat> Build(IEnumerable<GameEvent> events)
    {
        List<Beat> beats = [];
        string? lastGroup = null;
        foreach (GameEvent gameEvent in events)
        {
            if (StepFor(gameEvent) is not Step step)
            {
                continue;
            }

            string? group = step switch
            {
                MoveStep when gameEvent is UnitStepped stepped => $"tick {stepped.Tick}",
                AppearStep => "appear",
                _ => null,
            };
            if (group is not null && group == lastGroup)
            {
                beats[^1] = new Beat(beats[^1].Steps.Add(step));
            }
            else
            {
                beats.Add(new Beat([step]));
            }

            lastGroup = group;
        }

        return [.. beats];
    }

    /// <summary>The step for one event, or <c>null</c> for an <see cref="Ignored"/> event.</summary>
    /// <exception cref="ArgumentException">A new event type has no mapping yet.</exception>
    public static Step? StepFor(GameEvent gameEvent) => gameEvent switch
    {
        UnitStepped e => new MoveStep(e.UnitId, e.From, e.To),
        ClashMarked e => new ClashStep(e.UnitA, e.UnitB, e.Tile),
        UnitAttacked e => new StrikeStep(e.AttackerId, e.TargetId, e.Kind, e.Damage, e.HpAfter),
        UnitHealed e => new HealStep(e.UnitId, e.Amount, e.HpAfter),
        UnitDied e => new DeathStep(e.UnitId),
        UnitPlaced e => new AppearStep(e.UnitId, e.Owner, e.Type, e.Tile),
        UnitArrived e => new AppearStep(e.UnitId, e.Owner, e.Type, e.Tile),
        UnitSummoned e => new AppearStep(e.UnitId, e.Owner, e.Type, e.Tile),
        StatusApplied e => new StatusStep(e.UnitId, e.Status, true),
        StatusRemoved e => new StatusStep(e.UnitId, e.Status, false),
        AbilityUsed e => new AbilityStep(e.UnitId, e.Ability, e.Target),
        TileChanged e => new TileStep(e.Tile, e.Terrain),
        TurnStarted e => new BannerStep($"Turn {e.Turn}"),
        MatchEnded e => new BannerStep(e.Winner is null ? "Draw" : $"{e.Winner} wins ({e.Reason})"),
        _ when Ignored.Contains(gameEvent.GetType()) => null,
        _ => throw new ArgumentException($"No playback for {gameEvent.GetType().Name}.", nameof(gameEvent)),
    };
}
