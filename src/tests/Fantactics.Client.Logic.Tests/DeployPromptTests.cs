using Fantactics.Client.Logic.Board;
using Fantactics.Client.Logic.Input;
using Fantactics.Core;
using Fantactics.Core.Engine;
using Fantactics.Core.Geometry;
using Fantactics.Core.Hosting;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Client.Logic.Tests;

/// <summary>Affordable reserves are called out in the prompt, the roster title, and the board.</summary>
public class DeployPromptTests
{
    [Fact]
    public void ArrivalTilesCoverEveryReadyReserveUntilChosen()
    {
        (SeatUpdate update, MoveOptions options) = FirstWithDeploys();
        MoveOrderBuilder builder = new(update.View, options, RulesConfig.Default);

        Assert.Equal(options.Deploys.Select(d => d.UnitId), builder.ReadyToDeploy.Select(d => d.UnitId));
        Assert.Equal(
            options.Deploys.SelectMany(d => d.Tiles).ToHashSet(),
            builder.ArrivalTiles.ToHashSet());

        DeployOption first = options.Deploys[0];
        Assert.True(builder.Select(first.UnitId));
        Assert.Null(builder.Choose(first.Tiles[0]));

        Assert.DoesNotContain(builder.ReadyToDeploy, d => d.UnitId == first.UnitId);
        Assert.All(builder.ReadyToDeploy, d => Assert.True(d.Cost <= builder.CommandLeft));
        Assert.DoesNotContain(first.Tiles[0], builder.ArrivalTiles);
    }

    [Fact]
    public void NothingIsReadyOnceArrivalsRunOut()
    {
        (SeatUpdate update, MoveOptions options) = FirstWithDeploys();
        MoveOrderBuilder builder = new(update.View, options with { MaxArrivals = 1 }, RulesConfig.Default);

        DeployOption first = options.Deploys[0];
        builder.Select(first.UnitId);
        Assert.Null(builder.Choose(first.Tiles[0]));

        Assert.Empty(builder.ReadyToDeploy);
        Assert.Empty(builder.ArrivalTiles);
    }

    [Fact]
    public void TheBoardMarksArrivalTilesOnlyWithNothingSelected()
    {
        (SeatUpdate update, MoveOptions options) = FirstWithDeploys();
        MoveOrderBuilder builder = new(update.View, options, RulesConfig.Default);
        Point arrival = builder.ArrivalTiles.First();

        BoardModel idle = BoardModel.Build(update.View, RulesConfig.Default, builder, null, arrival);
        Assert.True(idle.Marks[arrival].HasFlag(TileMark.Arrival));
        Assert.Contains("reserve arrival tile", idle.Hint);

        UnitMoveOptions mover = options.Units.First(u => u.Destinations.Length > 0);
        builder.Select(mover.UnitId);
        BoardModel selecting = BoardModel.Build(update.View, RulesConfig.Default, builder, null, null);
        Assert.DoesNotContain(selecting.Marks.Values, mark => mark.HasFlag(TileMark.Arrival));
    }

    [Fact]
    public void ThePromptAndRosterTitleMentionReservesOnlyWhenOneCanDeploy()
    {
        (SeatUpdate ready, MoveOptions options) = FirstWithDeploys();
        DecisionInput input = new(ready, RulesConfig.Default);
        Assert.Contains("Reserves ready", HudText.Prompt(ready, RulesConfig.Default, _ => ""));
        Assert.Equal($"Deploy (Command {options.Command}):", input.RosterTitle);

        SeatUpdate none = Movement().First(u => u.Legal?.Moves is { Deploys.IsEmpty: true });
        Assert.DoesNotContain("Reserves", HudText.Prompt(none, RulesConfig.Default, _ => ""));
        Assert.Equal("", new DecisionInput(none, RulesConfig.Default).RosterTitle);

        (GameState state, Seat seat) = States.Along(6).First(s => s.State.Phase == Phase.Placement);
        Assert.Equal("Place:", new DecisionInput(Update(state, seat), RulesConfig.Default).RosterTitle);
    }

    [Fact]
    public void PromptsAndButtonsNameCharactersByTheirShortName()
    {
        Assert.Equal("Grukk (4)", DecisionInput.DeployLabel(RulesConfig.Default, new DeployOption(1, "WarLord", 4, [])));
        Assert.Equal("Wolf Rider (3)", DecisionInput.DeployLabel(RulesConfig.Default, new DeployOption(2, "WolfRider", 3, [])));

        SeatUpdate acting = new ulong[] { 1, 2, 3 }
            .SelectMany(States.Along)
            .Select(s => Update(s.State, s.Seat))
            .First(u => u.Legal?.Decision is ChooseUnitActionDecision);
        int unitId = ((ChooseUnitActionDecision)acting.Legal!.Decision).UnitId;
        SeatUpdate asWarLord = acting with
        {
            View = acting.View with
            {
                Units = [.. acting.View.Units.Select(u => u.Id == unitId ? u with { Type = "WarLord" } : u)],
            },
        };

        Assert.StartsWith("Grukk acts:", HudText.Prompt(asWarLord, RulesConfig.Default, _ => ""));
    }

    private static (SeatUpdate Update, MoveOptions Options) FirstWithDeploys()
    {
        SeatUpdate update = Movement().First(u => u.Legal?.Moves is { Deploys.IsEmpty: false });
        return (update, update.Legal?.Moves ?? throw new InvalidOperationException());
    }

    private static IEnumerable<SeatUpdate> Movement() => new ulong[] { 1, 2, 3, 4, 5, 6 }
        .SelectMany(States.Along)
        .Where(s => s.State.Phase == Phase.Movement)
        .Select(s => Update(s.State, s.Seat));

    private static SeatUpdate Update(GameState state, Seat seat) =>
        new(PlayerView.Project(state, seat), [], LegalActions.ForView(state, seat));
}
