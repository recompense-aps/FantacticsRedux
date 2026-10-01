using Fantactics.Client.Logic.Input;
using Fantactics.Core;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Geometry;
using Fantactics.Core.Hosting;
using Fantactics.Core.State;

namespace Fantactics.Client.Logic.Tests;

/// <summary>The per-decision input controller routes clicks and buttons to the right builder.</summary>
public class DecisionInputTests
{
    [Fact]
    public void EachDecisionGetsOnlyItsBuilder()
    {
        foreach ((GameState state, Seat seat) in States.Along(6).Take(60))
        {
            DecisionInput input = new(UpdateFor(state, seat));

            object?[] builders = [input.Draft, input.Placement, input.Moves, input.Actions];
            Assert.Single(builders, b => b is not null);
            object? expected = state.Phase switch
            {
                Phase.Draft => input.Draft,
                Phase.Placement => input.Placement,
                Phase.Movement => input.Moves,
                _ => input.Actions,
            };
            Assert.NotNull(expected);
            Assert.Equal(input.Placement is not null || input.Moves is not null, input.SubmitProblems is not null);
        }
    }

    [Fact]
    public void NoDecisionMeansNoInput()
    {
        (GameState state, Seat seat) = States.Along(6).First();
        DecisionInput input = new(new SeatUpdate(PlayerView.Project(state, seat), [], null));

        Assert.Null(input.Submit());
        Assert.Null(input.Click(new Point(0, 0), false));
        Assert.Null(input.Action("wait"));
        Assert.False(input.Cancel());
        Assert.Empty(input.ActionButtons);
        Assert.Empty(input.Roster);
        Assert.Null(input.SubmitProblems);
    }

    [Theory]
    [InlineData(Phase.Draft)]
    [InlineData(Phase.Placement)]
    public void ABotSuggestionSubmitsAsTheEngineAccepts(Phase phase)
    {
        (GameState state, Seat seat) = States.Along(7).First(s => s.State.Phase == phase);
        SeatUpdate update = UpdateFor(state, seat);
        DecisionInput input = new(update);
        Assert.Null(input.Submit());

        LegalActions legal = update.Legal ?? throw new InvalidOperationException();
        input.Suggest(States.CreateBot("captain", 1).Decide(update.View, legal.Decision, legal));

        ICommand command = input.Submit() ?? throw new InvalidOperationException("Nothing to submit.");
        Assert.IsType<Accepted>(GameEngine.Apply(state, seat, ViewIds.For(state, seat).ToEngine(command)));
    }

    [Fact]
    public void PlacementClicksPickUpAndRemoveUnits()
    {
        (GameState state, Seat seat) = States.Along(8).First(s => s.State.Phase == Phase.Placement);
        SeatUpdate update = UpdateFor(state, seat);
        DecisionInput input = new(update);
        LegalActions legal = update.Legal ?? throw new InvalidOperationException();
        input.Suggest(States.CreateBot("captain", 1).Decide(update.View, legal.Decision, legal));
        PlacementBuilder placement = input.Placement ?? throw new InvalidOperationException();
        (int unit, Point tile) = placement.Placed.First();
        Assert.Contains(("suggest", "Auto-place"), input.ActionButtons);
        Assert.Contains(input.Roster, r => r.UnitId == unit && r.Label.EndsWith('✓'));

        Assert.Null(input.Click(tile, secondary: false));
        Assert.Equal(unit, placement.Selected);
        Assert.True(input.Cancel());
        Assert.Null(placement.Selected);
        Assert.False(input.Cancel());

        Assert.Null(input.Click(tile, secondary: true));
        Assert.False(placement.Placed.ContainsKey(unit));
        Assert.Contains(input.Roster, r => r.UnitId == unit && !r.Label.EndsWith('✓'));
        Assert.NotEmpty(input.SubmitProblems ?? []);
        Assert.Null(input.Submit());
    }

    [Fact]
    public void MoveClicksSelectSendAndClearAUnit()
    {
        (GameState state, Seat seat, UnitMoveOptions unit) = States.Along(9)
            .Where(s => s.State.Phase == Phase.Movement)
            .SelectMany(s => (LegalActions.ForView(s.State, s.Seat)?.Moves?.Units ?? [])
                .Where(u => u.Destinations.Length > 0)
                .Select(u => (s.State, s.Seat, u)))
            .First();
        DecisionInput input = new(UpdateFor(state, seat));
        MoveOrderBuilder moves = input.Moves ?? throw new InvalidOperationException();
        Point from = PlayerView.Project(state, seat).Units.First(u => u.Id == unit.UnitId).Position;
        Point to = unit.Destinations[^1].Tile;

        Assert.Null(input.Click(from, secondary: false));
        Assert.Equal(unit.UnitId, moves.Selected);
        Assert.True(input.Cancel());
        Assert.False(input.Cancel());

        input.Click(from, secondary: false);
        Assert.Null(input.Click(to, secondary: false));
        Assert.True(moves.Moves.ContainsKey(unit.UnitId));
        Assert.Null(input.Message);
        if (input.Submit() is ICommand command)
        {
            Assert.IsType<Accepted>(GameEngine.Apply(state, seat, ViewIds.For(state, seat).ToEngine(command)));
        }

        input.Click(from, secondary: true);
        Assert.False(moves.Moves.ContainsKey(unit.UnitId));
        Assert.Null(moves.Selected);
    }

    [Theory]
    [InlineData(10UL)]
    [InlineData(11UL)]
    public void ActionInputReturnsTheChosenCommand(ulong seed)
    {
        int checkedStates = 0;
        foreach ((GameState state, Seat seat) in States.Along(seed).Where(s => s.State.Phase == Phase.Action))
        {
            SeatUpdate update = UpdateFor(state, seat);
            LegalActions legal = update.Legal ?? throw new InvalidOperationException();
            DecisionInput input = new(update);
            ActionPicker picker = input.Actions ?? throw new InvalidOperationException();

            Assert.Equal(picker.Abilities.Length, input.ActionButtons.Count(b => int.TryParse(b.Action, out _)));
            Assert.Equal(picker.Wait?.Command, input.Action("wait"));
            Assert.Equal(picker.Delay?.Command, input.Action("delay"));
            Assert.Equal(picker.Wait is not null, input.ActionButtons.Contains(("wait", "Wait (W)")));
            Assert.Equal(picker.Delay is not null, input.ActionButtons.Contains(("delay", "Delay (D)")));
            Assert.Null(input.Action("not an action"));

            foreach (Attack attack in legal.Actions.Select(o => o.Command).OfType<Attack>())
            {
                Point target = update.View.Units.First(u => u.Id == attack.TargetId).Position;
                Assert.Null(input.Click(target, secondary: true));
                Assert.Equal(picker.OptionAt(target)?.Command, input.Click(target, secondary: false));
            }

            checkedStates++;
        }

        Assert.True(checkedStates > 5);
    }

    [Fact]
    public void ARejectedSubmitShowsWhy()
    {
        (GameState state, Seat seat) = States.Along(6).First();
        DecisionInput input = new(UpdateFor(state, seat));

        input.Reject(new RuleViolation("test", "Not allowed."));

        Assert.Equal("Not allowed.", input.Message);
    }

    private static SeatUpdate UpdateFor(GameState state, Seat seat) =>
        new(PlayerView.Project(state, seat), [], LegalActions.ForView(state, seat));
}
