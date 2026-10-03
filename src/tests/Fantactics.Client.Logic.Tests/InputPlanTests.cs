using System.Globalization;
using Fantactics.Client.Logic.Drive;
using Fantactics.Client.Logic.Input;
using Fantactics.Client.Logic.Launch;
using Fantactics.Core;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Hosting;
using Fantactics.Core.State;

namespace Fantactics.Client.Logic.Tests;

/// <summary>
/// <c>--drive</c>'s input plans, played against <see cref="DecisionInput"/> the way <c>MatchScreen</c> routes
/// buttons, keys, and clicks, give commands the engine accepts.
/// </summary>
public class InputPlanTests
{
    [Theory]
    [InlineData(12UL)]
    [InlineData(13UL)]
    public void EveryPlanSubmitsACommandTheEngineAccepts(ulong seed)
    {
        HashSet<string> seen = [];
        foreach ((GameState state, Seat seat) in States.Along(seed))
        {
            SeatUpdate update = new(PlayerView.Project(state, seat), [], LegalActions.ForView(state, seat));
            LegalActions legal = update.Legal ?? throw new InvalidOperationException();
            ICommand choice = States.CreateBot("captain", (int)seed).Decide(update.View, legal.Decision, legal);

            ICommand command = Play(update, InputPlan.For(update, choice))
                ?? throw new InvalidOperationException($"The {InputPlan.DecisionName(update)} plan submitted nothing.");

            Assert.IsType<Accepted>(GameEngine.Apply(state, seat, ViewIds.For(state, seat).ToEngine(command)));
            seen.Add(InputPlan.DecisionName(update));
        }

        Assert.Equal(["action", "draft", "orders", "placement"], seen.Order());
    }

    [Fact]
    public void ActionPlansUseTheInputMapKeys()
    {
        (GameState state, Seat seat) = States.Along(14).First(s => s.State.Phase == Phase.Action);
        SeatUpdate update = new(PlayerView.Project(state, seat), [], LegalActions.ForView(state, seat));
        LegalActions legal = update.Legal ?? throw new InvalidOperationException();
        int unit = ((ChooseUnitActionDecision)legal.Decision).UnitId;

        Assert.Contains(InputStep.Press("wait_action"), InputPlan.For(update, new Wait(unit)));
        Assert.Contains(InputStep.Press("delay_action"), InputPlan.For(update, new Delay(unit)));
        Assert.Throws<ArgumentException>(() => InputPlan.For(update, SubmitMoveOrders.HoldAll));
    }

    [Fact]
    public void TheMenuRouteEndsOnStart()
    {
        Assert.Equal(InputStep.Button("Start"), InputPlan.Menu("main")[^1]);
        Assert.Contains(InputStep.Button("New match…"), InputPlan.Menu("main"));
        Assert.DoesNotContain(InputStep.Button("New match…"), InputPlan.Menu("new"));
    }

    [Fact]
    public void DriveParsesAndExcludesAutoplay()
    {
        LaunchArgs args = LaunchArgs.Parse(["--drive", "--shots", "shots"]);

        Assert.True(args.Drive);
        Assert.True(args.IsSmokeRun);
        Assert.False(args.SkipsMenu);
        Assert.Equal("shots", args.Shots);
        Assert.True(LaunchArgs.Parse(["--drive", "--p2", "human"]).SkipsMenu);
        Assert.Throws<ArgumentException>(() => LaunchArgs.Parse(["--drive", "--autoplay"]));
        Assert.Throws<ArgumentException>(() => LaunchArgs.Parse(["--drive", "--menu", "load"]));
    }

    /// <summary>Plays the steps the way the match screen routes them; returns the first command submitted.</summary>
    private static ICommand? Play(SeatUpdate update, IReadOnlyList<InputStep> steps)
    {
        DecisionInput input = new(update);
        LegalActions legal = update.Legal ?? throw new InvalidOperationException();
        foreach (InputStep step in steps)
        {
            ICommand? submitted = step.Kind switch
            {
                InputStepKind.ClickTile => input.Click(step.Tile, step.Secondary),
                InputStepKind.PressAction => Press(input, step.Name),
                InputStepKind.ClickButton => Button(input, legal, update, step.Name),
                _ => null,
            };
            if (submitted is not null)
            {
                return submitted;
            }
        }

        return null;
    }

    private static ICommand? Press(DecisionInput input, string action)
    {
        switch (action)
        {
            case "submit":
                return input.Submit();
            case "cancel":
                Assert.True(input.Cancel(), "Esc had no pick to drop.");
                return null;
            case "wait_action":
                return input.Action("wait");
            case "delay_action":
                return input.Action("delay");
            default:
                Assert.StartsWith("ability_", action);
                int slot = int.Parse(action["ability_".Length..], CultureInfo.InvariantCulture) - 1;
                return input.Action(slot.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static ICommand? Button(DecisionInput input, LegalActions legal, SeatUpdate update, string text)
    {
        switch (text)
        {
            case InputPlan.BotPick:
            case InputPlan.AutoPlace:
                input.Suggest(States.CreateBot("captain", 1).Decide(update.View, legal.Decision, legal));
                return null;
            case InputPlan.ClearDraft:
                input.ChangeDraft(draft =>
                {
                    draft.Clear();
                    return null;
                });
                return null;
            case InputPlan.SubmitDraft:
            case InputPlan.Submit:
                return input.Submit();
        }

        (int UnitId, string Label, bool Chosen)[] roster = [.. input.Roster
            .Where(r => r.Label == text)
            .OrderBy(r => r.Chosen)];
        Assert.True(roster.Length > 0, $"No button '{text}'.");
        input.ChooseRosterUnit(roster[0].UnitId);
        return null;
    }
}
