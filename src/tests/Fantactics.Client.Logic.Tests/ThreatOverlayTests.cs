using Fantactics.Client.Logic.Board;
using Fantactics.Core;
using Fantactics.Core.Engine;
using Fantactics.Core.Geometry;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Client.Logic.Tests;

/// <summary>The enemy threat overlay: what it marks, and that the seat's view is enough to compute it.</summary>
public class ThreatOverlayTests
{
    private static readonly RulesConfig _rules = RulesConfig.Default;

    [Fact]
    public void ThreatsFromTheViewMatchThoseFromTheFullState()
    {
        int checkedUnits = 0;
        foreach ((GameState state, Seat seat) in Fielded())
        {
            PlayerView view = PlayerView.Project(state, seat);
            foreach (Unit enemy in state.FieldUnits.Where(unit => state.AreEnemies(unit.Owner, seat)))
            {
                // View ids differ from engine ids; a field unit is the only one on its tile.
                int viewId = view.Units.Single(unit => unit.IsOnField && unit.Position == enemy.Position).Id;
                ThreatOverlay overlay = ThreatOverlay.For(view, _rules, [viewId]);
                IReadOnlySet<Point> reach = ThreatRange.Reach(state, enemy);

                Assert.True(reach.SetEquals(overlay.Move));
                Assert.True(ThreatRange.Strike(state, enemy).Except(reach).ToHashSet().SetEquals(overlay.Attack));
                checkedUnits++;
            }
        }

        Assert.True(checkedUnits > 0);
    }

    [Fact]
    public void HoveringAnEnemyShowsOnlyItsThreats()
    {
        PlayerView view = Views().First(v => Enemies(v).Count() >= 2);
        Unit[] enemies = [.. Enemies(view)];
        ThreatOverlay first = ThreatOverlay.For(view, _rules, [enemies[0].Id]);

        BoardModel model = BoardModel.Build(view, _rules, null, null, enemies[0].Position, threats: true);

        Assert.True(first.Move.SetEquals(Marked(model, TileMark.ThreatMove)));
        Assert.True(first.Attack.SetEquals(Marked(model, TileMark.ThreatAttack)));
    }

    [Fact]
    public void TheToggleShowsEveryEnemysThreats()
    {
        PlayerView view = Views().First(v => Enemies(v).Count() >= 2);
        ThreatOverlay all = ThreatOverlay.For(view, _rules, Enemies(view).Select(unit => unit.Id));
        Point own = view.Units.First(unit => unit.IsOnField && unit.Owner == view.Seat).Position;

        BoardModel on = BoardModel.Build(view, _rules, null, null, own, threats: true);
        BoardModel off = BoardModel.Build(view, _rules, null, null, own);

        Assert.NotEmpty(all.Move);
        Assert.True(all.Move.SetEquals(Marked(on, TileMark.ThreatMove)));
        Assert.True(all.Attack.SetEquals(Marked(on, TileMark.ThreatAttack)));
        Assert.Empty(Marked(off, TileMark.ThreatMove | TileMark.ThreatAttack));
    }

    [Fact]
    public void ATileIsNeverMarkedAsBothMoveAndAttack()
    {
        Assert.All(Views().Take(40), view =>
        {
            BoardModel model = BoardModel.Build(view, _rules, null, null, null, threats: true);
            Assert.DoesNotContain(model.Marks.Values, mark => mark.HasFlag(TileMark.ThreatMove | TileMark.ThreatAttack));
        });
    }

    [Fact]
    public void OwnUnitsAreNeverShownAsThreats()
    {
        PlayerView view = Views().First();
        IEnumerable<int> own = view.Units
            .Where(unit => unit.IsOnField && unit.Owner == view.Seat)
            .Select(unit => unit.Id);

        ThreatOverlay overlay = ThreatOverlay.For(view, _rules, own);

        Assert.Empty(overlay.Move);
        Assert.Empty(overlay.Attack);
    }

    /// <summary>States along bot matches where the deciding seat has units and enemies on the field.</summary>
    private static IEnumerable<(GameState State, Seat Seat)> Fielded() =>
        new ulong[] { 1, 2 }
            .SelectMany(States.Along)
            .Where(s => s.State.Phase is Phase.Movement or Phase.Action)
            .Where(s => s.State.FieldUnits.Any(unit => unit.Owner == s.Seat))
            .Where(s => s.State.FieldUnits.Any(unit => s.State.AreEnemies(unit.Owner, s.Seat)));

    private static IEnumerable<PlayerView> Views() => Fielded().Select(s => PlayerView.Project(s.State, s.Seat));

    private static IEnumerable<Unit> Enemies(PlayerView view) =>
        view.Units.Where(unit => unit.IsOnField && view.AreEnemies(unit.Owner, view.Seat));

    private static HashSet<Point> Marked(BoardModel model, TileMark mark) =>
        model.Marks
            .Where(pair => (pair.Value & mark) != 0)
            .Select(pair => pair.Key)
            .ToHashSet();
}
