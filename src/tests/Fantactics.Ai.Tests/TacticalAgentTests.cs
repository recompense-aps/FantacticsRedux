using System.Text.Json;
using Fantactics.Ai.Belief;
using Fantactics.Ai.Profiles;
using Fantactics.Core;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Players;
using Fantactics.Core.Rules;
using Fantactics.Core.Scenarios;
using Fantactics.Core.Serialization;
using Fantactics.Core.State;

namespace Fantactics.Ai.Tests;

/// <summary>The configurable bot: legal, deterministic, honest, and better than random.</summary>
public class TacticalAgentTests
{
    public static IEnumerable<object[]> Seeds => Enumerable.Range(1, 10).Select(seed => new object[] { seed });

    [Theory]
    [MemberData(nameof(Seeds))]
    public void BotMatchesKeepInvariantsAndEnd(int seed)
    {
        List<string> problems = [];
        string opponent = seed % 2 == 0 ? "captain@novice" : "random";

        MatchResult result = MatchRunner.Run(
            RulesConfig.Default,
            TestMatches.Riverford((ulong)seed),
            TestMatches.Bots("captain", opponent, seed),
            (state, _) => problems.AddRange(Invariants.Check(state).Select(p => $"turn {state.Turn}: {p}")),
            keepRecord: false);

        Assert.Empty(problems);
        Assert.InRange(result.Turns, 1, RulesConfig.Default.TurnLimit);
    }

    [Fact]
    public void SameSeedsProduceIdenticalMatchesEvenInParallel()
    {
        string[] hashes = Enumerable.Range(0, 4)
            .AsParallel()
            .Select(_ => StateHash.Compute(MatchRunner.Run(
                RulesConfig.Default,
                TestMatches.Riverford(5),
                TestMatches.Bots("captain", "captain@easy", 5),
                keepRecord: false).FinalState))
            .ToArray();

        Assert.Single(hashes.Distinct());
    }

    [Fact]
    public void CaptainBeatsRandomWithEitherRaceAndSeat()
    {
        int captainWins = Enumerable.Range(1, 20)
            .AsParallel()
            .Count(seed =>
            {
                bool captainIsP1 = seed % 2 == 0;
                (string p1Race, string p2Race) = seed % 4 < 2 ? ("Elves", "Goblins") : ("Goblins", "Elves");
                MatchResult result = MatchRunner.Run(
                    RulesConfig.Default,
                    TestMatches.Riverford((ulong)seed, p1Race, p2Race),
                    captainIsP1
                        ? TestMatches.Bots("captain", "random", seed)
                        : TestMatches.Bots("random", "captain", seed),
                    keepRecord: false);
                return result.Outcome.Won(captainIsP1 ? Seat.P1 : Seat.P2);
            });

        Assert.True(captainWins >= 18, $"Captain won only {captainWins} of 20.");
    }

    [Fact]
    public void DecisionsIgnoreHiddenInformation()
    {
        GameState state = MovementPhaseOfTurn(3);
        GameState withSecrets = WithHiddenChanges(state);

        string plain = Decide(state);
        string secret = Decide(withSecrets);

        Assert.NotEqual(StateHash.Compute(state), StateHash.Compute(withSecrets));
        Assert.Equal(plain, secret);
    }

    [Fact]
    public void BeliefStateMatchesTheViewAndTheReserveValue()
    {
        GameState state = MovementPhaseOfTurn(3);
        PlayerView view = PlayerView.Project(state, Seat.P1);

        GameState belief = BeliefState.From(view, RulesConfig.Default, ReserveGuesser.Guess(view, RulesConfig.Default));

        Assert.Equal(state.Turn, belief.Turn);
        Assert.Equal(state.Phase, belief.Phase);
        Assert.Equal(
            state.FieldUnits.Select(unit => (unit.Owner, unit.Type, unit.Position, unit.Hp)).Order(),
            belief.FieldUnits.Select(unit => (unit.Owner, unit.Type, unit.Position, unit.Hp)).Order());
        Assert.Equal(UnitRules.ArmyValue(state, Seat.P2), UnitRules.ArmyValue(belief, Seat.P2));
        Assert.Equal(UnitRules.ArmyValue(state, Seat.P1), UnitRules.ArmyValue(belief, Seat.P1));
        Assert.Equal(
            LegalActions.For(state, Seat.P1)?.Moves?.Units.Length,
            LegalActions.For(belief, Seat.P1)?.Moves?.Units.Length);
    }

    [Fact]
    public void PrefersAKillOverChipDamage()
    {
        GameState state = new ScenarioBuilder()
            .WithMap(
                ".......",
                ".......",
                ".......",
                ".......",
                ".......")
            .AddUnit(Seat.P1, "Archer", 0, 0, out int archer)
            .AddUnit(Seat.P2, "Grunt", 3, 0, out int grunt)
            .Modify(unit => unit with { Hp = 2 })
            .AddUnit(Seat.P2, "Bruiser", 0, 3)
            .Build();
        state = Accept(Accept(state, Seat.P1, SubmitMoveOrders.HoldAll), Seat.P2, SubmitMoveOrders.HoldAll);
        Assert.Equal(archer, state.TurnState.CurrentActor);

        ICommand choice = Decide(state, Seat.P1);

        Assert.Equal(new Attack(archer, grunt), choice);
    }

    [Fact]
    public void EveryProfileLoadsAtEveryDifficulty()
    {
        IEnumerable<string> specs = BotLibrary.Names
            .Where(name => name != BotLibrary.RandomBot)
            .SelectMany(name => BotLibrary.Difficulties.Select(difficulty => $"{name}@{difficulty}"));

        foreach (string spec in specs)
        {
            Assert.True(BotLibrary.IsKnown(spec));
            BotProfile profile = BotLibrary.Profile(spec);
            Assert.Equal(spec[(spec.IndexOf('@') + 1)..], profile.Difficulty);
        }

        Assert.False(BotLibrary.IsKnown("captain@impossible"));
        Assert.False(BotLibrary.IsKnown("nobody"));
        Assert.False(BotLibrary.IsKnown("random@hard"));
    }

    private static GameState MovementPhaseOfTurn(int turn)
    {
        GameState? found = null;
        MatchRunner.Run(
            RulesConfig.Default,
            TestMatches.Riverford(9),
            TestMatches.Bots("captain", "captain", 9),
            (state, _) =>
            {
                if (found is null
                    && state.Turn == turn
                    && state.Phase == Phase.Movement
                    && state.PendingOrders.IsEmpty
                    && UnitRules.ReserveValue(state, Seat.P2) > 0)
                {
                    found = state;
                }
            },
            keepRecord: false);
        return found ?? throw new InvalidOperationException($"Turn {turn} never reached a fresh movement phase.");
    }

    /// <summary>P2 locks in orders and swaps its reserve for a different one of the same value.</summary>
    private static GameState WithHiddenChanges(GameState state)
    {
        Dictionary<string, string> sameCost = new()
        {
            ["Bruiser"] = "Tank",
            ["Tank"] = "Mauler",
            ["Mauler"] = "Bruiser",
        };
        GameState locked = Accept(state, Seat.P2, SubmitMoveOrders.HoldAll);
        IEnumerable<Unit> swapped = locked.Units.Values
            .Where(unit => unit.Owner == Seat.P2
                && unit.Location == UnitLocation.Reserve
                && sameCost.ContainsKey(unit.Type))
            .Select(unit => unit with { Type = sameCost[unit.Type] });
        return locked with
        {
            Units = locked.Units.SetItems(swapped.Select(unit => KeyValuePair.Create(unit.Id, unit))),
        };
    }

    private static string Decide(GameState state) =>
        JsonSerializer.Serialize(Decide(state, Seat.P1), CoreJson.Options);

    private static ICommand Decide(GameState state, Seat seat)
    {
        TacticalAgent agent = new(BotLibrary.Profile("captain"), RulesConfig.Default, seed: 1);
        return agent.DecideFor(state, seat);
    }

    private static GameState Accept(GameState state, Seat seat, ICommand command) =>
        GameEngine.Apply(state, seat, command) is Accepted accepted
            ? accepted.State
            : throw new InvalidOperationException($"{command} was rejected.");
}
