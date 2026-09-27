using System.Collections.Immutable;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Events;
using Fantactics.Core.Geometry;
using Fantactics.Core.Scenarios;
using Fantactics.Core.State;

namespace Fantactics.Core.Tests;

/// <summary>Helpers for driving the engine in tests.</summary>
internal static class Play
{
    /// <summary>A 7×5 map of plains.</summary>
    public static readonly string[] OpenField =
    [
        ".......",
        ".......",
        ".......",
        ".......",
        ".......",
    ];

    /// <summary>
    /// Adds reserves to both seats (Elves 12, Goblins 11) so neither falls below the rout threshold, for scenarios
    /// that must last more than one turn.
    /// </summary>
    public static ScenarioBuilder WithBallast(this ScenarioBuilder builder) => builder
        .AddReserve(Seat.P1, "Druid", out _)
        .AddReserve(Seat.P1, "Ranger", out _)
        .AddReserve(Seat.P2, "WarLord", out _)
        .AddReserve(Seat.P2, "Shaman", out _);

    /// <summary>Applies a command and asserts it was accepted.</summary>
    public static Accepted Apply(GameState state, Seat seat, ICommand command)
    {
        ApplyResult result = GameEngine.Apply(state, seat, command);
        return result as Accepted
            ?? throw new Xunit.Sdk.XunitException($"Expected accepted, got {result}");
    }

    /// <summary>Applies a command and asserts it was rejected with <paramref name="code"/>.</summary>
    public static void Rejects(GameState state, Seat seat, ICommand command, string code)
    {
        Rejected rejected = Assert.IsType<Rejected>(GameEngine.Apply(state, seat, command));
        Assert.Equal(code, rejected.Violation.Code);
    }

    /// <summary>Submits both seats' move orders and returns the resolved state with all events.</summary>
    public static (GameState State, ImmutableArray<GameEvent> Events) Moves(
        GameState state, SubmitMoveOrders p1, SubmitMoveOrders p2)
    {
        Accepted first = Apply(state, Seat.P1, p1);
        Accepted second = Apply(first.State, Seat.P2, p2);
        return (second.State, [.. first.Events, .. second.Events]);
    }

    /// <summary>Move orders for one seat.</summary>
    public static SubmitMoveOrders Orders(params MoveOrder[] moves) => new([.. moves], []);

    /// <summary>A move order along the given tiles.</summary>
    public static MoveOrder Move(int unitId, params (int X, int Y)[] path) =>
        new(unitId, [.. path.Select(step => new Point(step.X, step.Y))]);

    /// <summary>Makes every unit in the action order wait until the turn ends.</summary>
    public static (GameState State, List<GameEvent> Events) WaitOutTurn(GameState state)
    {
        List<GameEvent> events = [];
        int turn = state.Turn;
        while (state.Phase == Phase.Action && state.Turn == turn && state.TurnState.CurrentActor is int actor)
        {
            Accepted accepted = Apply(state, state.Units[actor].Owner, new Wait(actor));
            state = accepted.State;
            events.AddRange(accepted.Events);
        }

        return (state, events);
    }
}
