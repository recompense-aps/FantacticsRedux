using System.Collections.Immutable;
using Fantactics.Client.Logic.Board;
using Fantactics.Client.Logic.Input;
using Fantactics.Client.Logic.Menus;
using Fantactics.Client.Logic.Session;
using Fantactics.Core;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Geometry;
using Fantactics.Core.Hosting;
using Fantactics.Core.Players;
using Fantactics.Core.Records;
using Fantactics.Core.Rules;
using Fantactics.Core.State;
using Fantactics.Protocol.Files;

namespace Fantactics.Client.Logic.Tests;

/// <summary>The draft and placement screens' builders, the new-match form, and save summaries.</summary>
public class DraftTests
{
    private static readonly RulesConfig _rules = RulesConfig.Default;

    [Theory]
    [InlineData(1UL, 40, 30)]
    [InlineData(2UL, 25, 12)]
    [InlineData(3UL, 60, 45)]
    public void RandomDraftsAreAcceptedExactlyWhenTheyHaveNoProblems(ulong seed, int budget, int cap)
    {
        Random random = new((int)seed);
        MatchSetup setup = States.Setup(seed) with
        {
            DraftBudgets = ImmutableSortedDictionary.CreateRange([KeyValuePair.Create(Seat.P1, budget)]),
            StartingCaps = ImmutableSortedDictionary.CreateRange([KeyValuePair.Create(Seat.P1, cap)]),
        };
        GameState state = setup.CreateInitialState(_rules);
        DraftOptions options = LegalActions.ForView(state, Seat.P1)?.Draft ?? throw new InvalidOperationException();
        Assert.Equal(budget, options.Budget);
        Assert.Equal(cap, options.StartingCap);

        for (int draft = 0; draft < 40; draft++)
        {
            DraftBuilder builder = new(options);
            for (int click = 0; click < 15; click++)
            {
                string type = options.Units[random.Next(options.Units.Length)].Type;
                bool reserve = random.Next(2) == 0;
                switch (random.Next(4))
                {
                    case 0:
                        builder.Remove(type, reserve);
                        break;
                    case 1:
                        builder.Move(type, reserve);
                        break;
                    default:
                        Assert.Equal(builder.WhyNot(type, reserve), builder.Add(type, reserve));
                        break;
                }

                Assert.True(builder.Remaining >= 0);
                Assert.True(builder.StartingCost <= cap);
            }

            ApplyResult result = GameEngine.Apply(state, Seat.P1, builder.Build());
            Assert.Equal(builder.Problems.Count == 0, result is Accepted);
        }
    }

    [Fact]
    public void UniqueUnitsAndTheStartingCapAreEnforcedAsYouClick()
    {
        DraftOptions options = LegalActions.ForView(States.Setup(1).CreateInitialState(_rules), Seat.P1)?.Draft
            ?? throw new InvalidOperationException();
        DraftUnitOption unique = options.Units.First(unit => unit.Unique);
        DraftBuilder builder = new(options with { StartingCap = unique.Cost });

        Assert.Null(builder.Add(unique.Type, reserve: false));
        Assert.NotNull(builder.Add(unique.Type, reserve: true));
        string cheap = options.Units.Where(unit => !unit.Unique).MinBy(unit => unit.Cost)?.Type ?? "";
        Assert.NotNull(builder.Add(cheap, reserve: false));
        Assert.Null(builder.Add(cheap, reserve: true));
        Assert.NotNull(builder.Move(cheap, fromReserve: true));
        Assert.Null(builder.Move(unique.Type, fromReserve: false));
        Assert.Equal(["The starting army needs at least one unit."], builder.Problems);
    }

    [Fact]
    public void ABotsDraftAndPlacementLoadIntoTheBuildersUnchanged()
    {
        MatchHost host = new(_rules, States.Setup(5));
        foreach (Seat seat in SeatExtensions.All)
        {
            SubmitDraft draft = (SubmitDraft)States.CreateBot("captain", (int)seat).DecideFor(host.State, seat);
            DraftBuilder builder = new(LegalActions.ForView(host.State, seat)?.Draft ?? throw new InvalidOperationException());
            builder.Load(draft);
            Assert.Equal(draft.Starting.AsEnumerable(), builder.Build().Starting);
            Assert.Equal(draft.Reserve.AsEnumerable(), builder.Build().Reserve);
            host.SubmitEngine(seat, draft);
        }

        Assert.Equal(Phase.Placement, host.State.Phase);
        PlayerView view = PlayerView.Project(host.State, Seat.P1);
        LegalActions legal = LegalActions.ForView(host.State, Seat.P1) ?? throw new InvalidOperationException();
        var placement = (PlaceStartingArmy)States.CreateBot("captain", 3).Decide(view, legal.Decision, legal);
        PlacementBuilder placer = new(view, legal.Placement ?? throw new InvalidOperationException());
        placer.Load(placement);

        Assert.Empty(placer.Problems);
        Assert.Null(placer.Selected);
        Assert.Equal(placement.Placements.OrderBy(p => p.UnitId), placer.Build().Placements);
    }

    [Theory]
    [InlineData(7UL)]
    [InlineData(8UL)]
    public void RandomPlacementClicksBuildAcceptedPlacements(ulong seed)
    {
        Random random = new((int)seed);
        GameState state = PlacementState(seed);
        PlayerView view = PlayerView.Project(state, Seat.P2);
        PlacementOptions options = LegalActions.ForView(state, Seat.P2)?.Placement ?? throw new InvalidOperationException();
        PlacementBuilder builder = new(view, options);

        for (int click = 0; click < 60; click++)
        {
            if (random.Next(5) == 0)
            {
                builder.Select(options.UnitIds[random.Next(options.UnitIds.Length)]);
            }
            else if (random.Next(8) == 0)
            {
                builder.Clear(options.UnitIds[random.Next(options.UnitIds.Length)]);
            }
            else
            {
                builder.Choose(options.Tiles[random.Next(options.Tiles.Length)]);
            }

            Assert.Equal(builder.Placed.Count, builder.Placed.Values.Distinct().Count());
        }

        while (builder.Selected is not null)
        {
            Assert.Null(builder.Choose(builder.FreeTiles.First()));
        }

        Assert.Empty(builder.Problems);
        ApplyResult result = GameEngine.Apply(state, Seat.P2, ViewIds.For(state, Seat.P2).ToEngine(builder.Build()));
        Assert.True(result is Accepted, (result as Rejected)?.Violation.Message);
    }

    [Fact]
    public void ChoosingATakenTileSwapsTheUnits()
    {
        GameState state = PlacementState(9);
        PlayerView view = PlayerView.Project(state, Seat.P1);
        PlacementOptions options = LegalActions.ForView(state, Seat.P1)?.Placement ?? throw new InvalidOperationException();
        PlacementBuilder builder = new(view, options);
        (int first, int second) = (options.UnitIds[0], options.UnitIds[1]);
        (Point a, Point b) = (options.Tiles[0], options.Tiles[1]);

        Assert.Equal(first, builder.Selected);
        Assert.Null(builder.Choose(a));
        Assert.Equal(second, builder.Selected);
        Assert.Null(builder.Choose(b));
        builder.Select(first);
        Assert.Null(builder.Choose(b));

        Assert.Equal(b, builder.Placed[first]);
        Assert.Equal(a, builder.Placed[second]);
        Assert.NotNull(builder.Choose(new Point(-1, -1)));

        BoardModel model = BoardModel.Build(view, _rules, null, null, null, builder);
        Assert.Equal(2, model.Tokens.Count(token => token.Mine));
        Assert.True(model.Marks[options.Tiles[2]].HasFlag(TileMark.Reachable));
        Assert.False(model.Marks.GetValueOrDefault(a).HasFlag(TileMark.Reachable));
    }

    [Fact]
    public void TheFormChecksItsSeatsAndWritesOnlyNonDefaultLimits()
    {
        NewMatchForm form = NewMatchForm.Defaults(_rules);
        Func<string, bool> isBot = spec => spec is "captain" or "captain@easy";

        Assert.Empty(form.Problems(_rules, isBot));
        MatchSetup setup = form.ToSetup(_rules, 42);
        Assert.Equal(42UL, setup.Seed);
        Assert.Null(setup.AllowedRaces);
        Assert.Null(setup.DraftBudgets);
        Assert.Equal("human", setup.Seats[Seat.P1]);

        NewMatchForm changed = form.WithSeat(Seat.P2, seat => seat with { Controller = "bot:nobody", Races = [], Budget = 50 });
        changed = changed with { Seed = 7, DraftAs = "captain" };
        Assert.Equal(2, changed.Problems(_rules, isBot).Count);
        MatchSetup custom = changed.WithSeat(Seat.P2, seat => seat with { Races = ["Elves"] }).ToSetup(_rules, 42);
        Assert.Equal(7UL, custom.Seed);
        Assert.Equal(new Dictionary<Seat, int> { [Seat.P2] = 50 }, custom.DraftBudgets ?? ImmutableSortedDictionary<Seat, int>.Empty);
        Assert.Equal(["Elves"], custom.AllowedRaces?[Seat.P2]);
    }

    [Fact]
    public void SaveSummariesDescribeMatchFilesAndSkipOthers()
    {
        string folder = Directory.CreateTempSubdirectory("fantactics-saves").FullName;
        try
        {
            MatchFiles.Write(Path.Combine(folder, "match.json"), new MatchHost(_rules, States.Setup(3, p2: "llm")).ToRecord());
            File.WriteAllText(Path.Combine(folder, "notes.json"), "{ \"hello\": 1 }");

            IReadOnlyList<SaveSummary> saves = SaveSummary.In(folder);

            Assert.Equal(2, saves.Count);
            SaveSummary match = saves.Single(save => save.Readable);
            Assert.Equal("llm", match.Seats[Seat.P2]);
            Assert.Equal("match.json: turn 0 Draft, human vs llm", match.Text);
            Assert.Equal("notes.json: not a match file", saves.Single(save => !save.Readable).Text);
            Assert.Empty(SaveSummary.In(Path.Combine(folder, "missing")));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void UnitTextDescribesADraftOption()
    {
        DraftUnitOption archer = new("Archer", 5, false, "Elves", ["Ranged"]);

        Assert.Equal("Elves · Ranged", UnitText.Tags(archer));
        Assert.Equal("Elves · unique", UnitText.Tags(archer with { Classes = [], Unique = true }));
        Assert.StartsWith("HP 7 · ATK 4", UnitText.Stats(_rules.Units["Archer"]));
        Assert.Equal("Pinning Shot", UnitText.Features(_rules.Units["Ranger"]));
    }

    /// <summary>A match where both bots have drafted and both seats owe their placement.</summary>
    private static GameState PlacementState(ulong seed)
    {
        MatchHost host = new(_rules, States.Setup(seed));
        foreach (Seat seat in SeatExtensions.All)
        {
            host.SubmitEngine(seat, States.CreateBot("captain", (int)seed).DecideFor(host.State, seat));
        }

        return host.State;
    }
}
