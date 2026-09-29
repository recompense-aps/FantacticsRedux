using System.Collections.Immutable;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Maps;
using Fantactics.Core.Rules;
using Fantactics.Core.State;
using static Fantactics.Core.Tests.Play;

namespace Fantactics.Core.Tests;

/// <summary>Per-player unit ids: nothing a seat is shown reveals the size of the enemy draft.</summary>
public class ViewIdsTests
{
    [Fact]
    public void IdsDoNotDependOnTheEnemyDraft()
    {
        PlayerView small = ViewAfterPlacement(["Archer", "Archer"], ["Scout"]);
        PlayerView large = ViewAfterPlacement(["Archer", "Archer", "Scout", "Scout", "Scout", "Herbalist"], ["Ranger"]);

        // P2's own ids start at 1, and the enemy's at 1001, whatever P1 drafted.
        Assert.Equal([1, 2, 3, 4], small.Units.Where(u => u.Owner == Seat.P2).Select(u => u.Id));
        Assert.Equal([1, 2, 3, 4], large.Units.Where(u => u.Owner == Seat.P2).Select(u => u.Id));
        Assert.Equal(
            Enumerable.Range(ViewIds.EnemyIdBase + 1, 2),
            small.Units.Where(u => u.Owner == Seat.P1).Select(u => u.Id));
        Assert.Equal(
            Enumerable.Range(ViewIds.EnemyIdBase + 1, 6),
            large.Units.Where(u => u.Owner == Seat.P1).Select(u => u.Id));
    }

    [Fact]
    public void ViewOptionsTranslateBackToAcceptedCommands()
    {
        GameState state = PlacedMatch(["Archer", "Scout"], ["Ranger"]);
        state = Apply(state, Seat.P1, SubmitMoveOrders.HoldAll).State;
        state = Apply(state, Seat.P2, SubmitMoveOrders.HoldAll).State;
        Seat acting = GameEngine.PendingDecisions(state)[0].Seat;

        LegalActions legal = Assert.IsType<LegalActions>(LegalActions.ForView(state, acting));
        ViewIds ids = ViewIds.For(state, acting);

        Assert.All(legal.Actions, option =>
            Assert.IsType<Accepted>(GameEngine.Apply(state, acting, ids.ToEngine(option.Command))));
    }

    [Fact]
    public void CommandsNamingUnknownUnitsAreRejected()
    {
        GameState state = PlacedMatch(["Archer", "Scout"], ["Ranger"]);
        ViewIds ids = ViewIds.For(state, Seat.P1);

        ICommand command = ids.ToEngine(new SubmitMoveOrders([new MoveOrder(99, [])], []));

        Assert.IsType<Rejected>(GameEngine.Apply(state, Seat.P1, command));
    }

    private static PlayerView ViewAfterPlacement(ImmutableArray<string> p1Starting, ImmutableArray<string> p1Reserve) =>
        PlayerView.Project(PlacedMatch(p1Starting, p1Reserve), Seat.P2);

    /// <summary>Elves (P1) with the given draft against a fixed Goblin draft, placed and at the start of turn 1.</summary>
    private static GameState PlacedMatch(ImmutableArray<string> p1Starting, ImmutableArray<string> p1Reserve)
    {
        GameState state = GameEngine.NewMatch(RulesConfig.Default, MapLibrary.Load("riverford"), 1);
        state = Apply(state, Seat.P1, new SubmitDraft(p1Starting, p1Reserve)).State;
        state = Apply(state, Seat.P2, new SubmitDraft(["Grunt", "Grunt", "Tank"], ["Rusher"])).State;
        return SeatExtensions.All.Aggregate(state, (current, seat) =>
        {
            PlacementOptions options = LegalActions.For(current, seat)?.Placement
                ?? throw new InvalidOperationException($"{seat} has nothing to place.");
            IEnumerable<UnitPlacement> placements = options.UnitIds
                .Select((id, index) => new UnitPlacement(id, options.Tiles[index]));
            return Apply(current, seat, new PlaceStartingArmy([.. placements])).State;
        });
    }
}
