using Fantactics.Client.Logic.Board;
using Fantactics.Client.Logic.Input;
using Fantactics.Core;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Geometry;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Client.Logic.Tests;

/// <summary>Clicks only ever build commands the engine accepts.</summary>
public class InputTests
{
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    public void RandomClicksBuildAcceptedMoveOrders(ulong seed)
    {
        Random random = new((int)seed);
        int checkedStates = 0;
        foreach ((GameState state, Seat seat) in States.Along(seed).Where(s => s.State.Phase == Phase.Movement))
        {
            PlayerView view = PlayerView.Project(state, seat);
            MoveOptions options = LegalActions.ForView(state, seat)?.Moves ?? throw new InvalidOperationException();
            MoveOrderBuilder builder = new(view, options);
            int[] selectable = [.. options.Units.Select(u => u.UnitId), .. options.Deploys.Select(d => d.UnitId)];
            for (int click = 0; click < 12 && selectable.Length > 0; click++)
            {
                if (builder.Select(selectable[random.Next(selectable.Length)]) && builder.Targets.ToArray() is { Length: > 0 } targets)
                {
                    builder.Choose(targets[random.Next(targets.Length)]);
                }
            }

            ApplyResult result = GameEngine.Apply(state, seat, ViewIds.For(state, seat).ToEngine(builder.Build()));
            Assert.True(
                builder.Problems.Count > 0 || result is Accepted,
                $"Built orders were rejected: {(result as Rejected)?.Violation.Message}");
            Assert.True(builder.Problems.Count == 0 || result is Rejected, "A reported problem was accepted.");
            checkedStates++;
        }

        Assert.True(checkedStates > 5);
    }

    [Theory]
    [InlineData(3UL)]
    [InlineData(4UL)]
    public void EveryActionOptionCanBeClicked(ulong seed)
    {
        foreach ((GameState state, Seat seat) in States.Along(seed).Where(s => s.State.Phase == Phase.Action))
        {
            PlayerView view = PlayerView.Project(state, seat);
            LegalActions legal = LegalActions.ForView(state, seat) ?? throw new InvalidOperationException();
            int unitId = ((ChooseUnitActionDecision)legal.Decision).UnitId;
            foreach (ActionOption option in legal.Actions)
            {
                ActionPicker picker = new(view, unitId, legal.Actions);
                ActionOption? picked = option.Command switch
                {
                    Attack attack => picker.OptionAt(view.Units.First(u => u.Id == attack.TargetId).Position),
                    UseAbility use => picker.SelectAbility(picker.Abilities.IndexOf(use.Ability))
                        ?? picker.OptionAt(use.Target ?? view.Units.First(u => u.Id == unitId).Position),
                    Wait => picker.Wait,
                    Delay => picker.Delay,
                    _ => null,
                };

                Assert.Equal(option, picked);
            }
        }
    }

    [Fact]
    public void TheBoardHighlightsWhereTheSelectedUnitCanGo()
    {
        (GameState state, Seat seat) = States.Along(5).First(s => s.State.Phase == Phase.Movement);
        PlayerView view = PlayerView.Project(state, seat);
        MoveOptions options = LegalActions.ForView(state, seat)?.Moves ?? throw new InvalidOperationException();
        MoveOrderBuilder builder = new(view, options);
        UnitMoveOptions unit = options.Units.First(u => u.Destinations.Length > 0);
        builder.Select(unit.UnitId);
        Point hover = unit.Destinations[^1].Tile;

        BoardModel board = BoardModel.Build(view, RulesConfig.Default, builder, null, hover);

        Assert.Equal(
            unit.Destinations.Select(d => d.Tile).Order(),
            board.Marks.Where(m => m.Value.HasFlag(TileMark.Reachable)).Select(m => m.Key).Order());
        Assert.Equal(unit.Destinations[^1].Path.Order(), board.Marks.Where(m => m.Value.HasFlag(TileMark.Path)).Select(m => m.Key).Order());
        Assert.Equal(view.Units.Count(u => u.IsOnField), board.Tokens.Length);

        builder.Select(unit.UnitId);
        Assert.Null(builder.Choose(hover));
        Assert.Single(BoardModel.Build(view, RulesConfig.Default, builder, null, null).Arrows);
    }
}
