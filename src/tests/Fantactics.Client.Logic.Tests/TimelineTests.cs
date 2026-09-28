using System.Runtime.CompilerServices;
using Fantactics.Client.Logic.Playback;
using Fantactics.Core;
using Fantactics.Core.Events;
using Fantactics.Core.Geometry;

namespace Fantactics.Client.Logic.Tests;

/// <summary>Events become beats to animate.</summary>
public class TimelineTests
{
    public static IEnumerable<object[]> EventTypes() => typeof(GameEvent).Assembly.GetTypes()
        .Where(type => type.IsSubclassOf(typeof(GameEvent)) && !type.IsAbstract)
        .Select(type => new object[] { type });

    [Theory]
    [MemberData(nameof(EventTypes))]
    public void EveryEventTypeHasAStepOrIsIgnoredOnPurpose(Type type)
    {
        GameEvent sample = (GameEvent)RuntimeHelpers.GetUninitializedObject(type);

        Step? step = TimelineBuilder.StepFor(sample);

        Assert.True(step is not null || TimelineBuilder.Ignored.Contains(type));
    }

    [Fact]
    public void OneTicksStepsAndConsecutiveArrivalsPlayTogether()
    {
        GameEvent[] events =
        [
            new UnitStepped(1, 1, new Point(0, 0), new Point(1, 0)),
            new UnitStepped(1, 1001, new Point(5, 0), new Point(4, 0)),
            new UnitStopped(1, 1, new Point(1, 0), default),
            new UnitStepped(2, 1, new Point(1, 0), new Point(2, 0)),
            new UnitAttacked(1, 1001, AttackKind.Basic, 3, 4),
            new UnitArrived(2, Seat.P1, "Scout", new Point(0, 1), 3, true),
            new UnitArrived(1002, Seat.P2, "Grunt", new Point(9, 1), 2, true),
            new TurnStarted(2, Seat.P1),
        ];

        Beat[] beats = [.. TimelineBuilder.Build(events)];

        Assert.Equal([2, 1, 1, 2, 1], beats.Select(beat => beat.Steps.Length));
        Assert.IsType<BannerStep>(beats[^1].Steps[0]);
    }
}
