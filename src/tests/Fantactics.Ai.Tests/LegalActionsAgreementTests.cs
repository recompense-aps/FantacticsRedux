using Fantactics.Core;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Ai.Tests;

/// <summary>
/// Every option <see cref="LegalActions"/> lists must be accepted by <see cref="GameEngine.Apply"/>, because bots and
/// LLMs trust the enumerator (Simulation §7, layer 2).
/// </summary>
public class LegalActionsAgreementTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void EveryListedOptionIsAccepted(int seed)
    {
        List<string> failures = [];
        GameState? previous = null;

        MatchRunner.Run(
            RulesConfig.Default,
            TestMatches.Alternating((ulong)seed),
            TestMatches.RandomAgents(seed),
            (state, _) =>
            {
                // Check the state the agents see, one command behind, so we test every decision point.
                previous = state;
                foreach (Decision decision in GameEngine.PendingDecisions(state))
                {
                    failures.AddRange(CheckOptions(state, decision.Seat));
                }
            });

        Assert.NotNull(previous);
        Assert.Empty(failures.Take(20));
    }

    private static IEnumerable<string> CheckOptions(GameState state, Seat seat)
    {
        LegalActions? legal = LegalActions.For(state, seat);
        if (legal is null)
        {
            return [$"No options for pending decision of {seat}."];
        }

        IEnumerable<ICommand> candidates = legal.Decision switch
        {
            ChooseUnitActionDecision => legal.Actions.Select(option => option.Command),
            SubmitMoveOrdersDecision => MoveCandidates(legal.Moves!),
            _ => [],
        };

        return candidates
            .Select(command => (Command: command, Result: GameEngine.Apply(state, seat, command)))
            .Where(pair => pair.Result is Rejected)
            .Select(pair => $"turn {state.Turn}: {pair.Command} rejected: {((Rejected)pair.Result).Violation.Message}");
    }

    private static IEnumerable<ICommand> MoveCandidates(MoveOptions moves)
    {
        IEnumerable<ICommand> singleMoves = moves.Units.SelectMany(unit => unit.Destinations
            .Select(destination => new SubmitMoveOrders([new MoveOrder(unit.UnitId, destination.Path)], [])));
        IEnumerable<ICommand> singleDeploys = moves.Deploys.SelectMany(option => option.Tiles
            .Select(tile => new SubmitMoveOrders([], [new DeployOrder(option.UnitId, tile)])));
        return singleMoves.Concat(singleDeploys);
    }
}
