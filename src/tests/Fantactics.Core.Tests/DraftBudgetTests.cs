using System.Collections.Immutable;
using System.Text.Json;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Maps;
using Fantactics.Core.Records;
using Fantactics.Core.Rules;
using Fantactics.Core.Scenarios;
using Fantactics.Core.Serialization;
using Fantactics.Core.State;
using static Fantactics.Core.Tests.Play;

namespace Fantactics.Core.Tests;

/// <summary>Per-seat draft budgets and starting caps set by the match setup (GameDesign §4.4).</summary>
public class DraftBudgetTests
{
    private static readonly SubmitDraft _sixtyPoints =
        new(["Ranger", "Ranger", "Ranger", "Ranger", "Ranger", "Ranger", "Ranger"], ["Ranger", "Ranger", "Ranger"]);

    [Fact]
    public void EachSeatDraftsWithinItsOwnBudgetAndCap()
    {
        GameState state = NewMatch(budgets: new() { [Seat.P1] = 60 }, caps: new() { [Seat.P1] = 45 });

        Assert.Equal(60, Draft(state, Seat.P1).Budget);
        Assert.Equal(45, Draft(state, Seat.P1).StartingCap);
        Assert.Equal(RulesConfig.Default.DraftBudget, Draft(state, Seat.P2).Budget);
        Assert.Equal(RulesConfig.Default.StartingCap, Draft(state, Seat.P2).StartingCap);
        Apply(state, Seat.P1, _sixtyPoints);
        Rejects(state, Seat.P2, _sixtyPoints, "over-starting-cap");
        Rejects(state, Seat.P2, new SubmitDraft(["Ranger"], [.. Enumerable.Repeat("Ranger", 7)]), "over-budget");
    }

    [Fact]
    public void BudgetsAndCapsMustBePositive()
    {
        Assert.Throws<ArgumentException>(() => NewMatch(budgets: new() { [Seat.P2] = 0 }));
        Assert.Throws<ArgumentException>(() => NewMatch(caps: new() { [Seat.P1] = -5 }));
    }

    [Theory]
    [InlineData(null, null, Seat.P2)]
    [InlineData(8, null, null)]
    [InlineData(8, 60, Seat.P1)]
    public void EachSeatRoutsAgainstItsOwnBudget(int? p1Budget, int? p2Budget, Seat? winner)
    {
        // P1's Scout is worth 2 and P2's Tanks 12: below 25% of 40 and above it, respectively.
        ScenarioBuilder builder = new ScenarioBuilder()
            .WithMap(OpenField)
            .AddUnit(Seat.P1, "Scout", 0, 0)
            .AddUnit(Seat.P2, "Tank", 6, 4)
            .AddUnit(Seat.P2, "Tank", 6, 3)
            .AddUnit(Seat.P2, "Tank", 6, 2);
        if (p1Budget is int p1)
        {
            builder.WithDraftBudget(Seat.P1, p1);
        }

        if (p2Budget is int p2)
        {
            builder.WithDraftBudget(Seat.P2, p2);
        }

        (GameState moved, _) = Moves(builder.Build(), SubmitMoveOrders.HoldAll, SubmitMoveOrders.HoldAll);
        (GameState after, _) = WaitOutTurn(moved);

        Assert.Equal(winner, after.Outcome?.Winners.Cast<Seat?>().SingleOrDefault());
    }

    [Fact]
    public void BudgetsReachTheViewAndSurviveTheSetupFile()
    {
        MatchSetup setup = new(
            "riverford",
            1,
            ImmutableSortedDictionary.CreateRange([
                KeyValuePair.Create(Seat.P1, "llm"),
                KeyValuePair.Create(Seat.P2, "llm")]),
            DraftBudgets: ImmutableSortedDictionary.CreateRange([KeyValuePair.Create(Seat.P2, 30)]),
            StartingCaps: ImmutableSortedDictionary.CreateRange([KeyValuePair.Create(Seat.P2, 20)]));

        string json = JsonSerializer.Serialize(setup, CoreJson.Options);
        MatchSetup? loaded = JsonSerializer.Deserialize<MatchSetup>(json, CoreJson.Options);
        PlayerSummary p2 = PlayerView.Project(setup.CreateInitialState(RulesConfig.Default), Seat.P1).Players[Seat.P2];

        Assert.Equal(30, loaded?.DraftBudgets?[Seat.P2]);
        Assert.Equal(20, loaded?.StartingCaps?[Seat.P2]);
        Assert.Equal(30, p2.DraftBudget);
        Assert.Equal(20, p2.StartingCap);
    }

    private static GameState NewMatch(Dictionary<Seat, int>? budgets = null, Dictionary<Seat, int>? caps = null) =>
        GameEngine.NewMatch(RulesConfig.Default, MapLibrary.Load("riverford"), 42, null, budgets, caps);

    private static DraftOptions Draft(GameState state, Seat seat) =>
        LegalActions.For(state, seat)?.Draft ?? throw new InvalidOperationException($"{seat} has no draft.");
}
