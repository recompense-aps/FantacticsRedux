using System.Collections.Immutable;
using Fantactics.Core;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Events;
using Fantactics.Core.Players;
using Fantactics.Core.Records;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Ai;

/// <summary>
/// Plays a whole match in memory by asking each seat's agent for its pending decision until the match ends
/// (Simulation §4). It is just another driver of <see cref="GameEngine"/>.
/// </summary>
public static class MatchRunner
{
    /// <summary>Safety limit on commands per match, so a rules bug can't loop forever.</summary>
    public const int DefaultMaxCommands = 20_000;

    /// <summary>Plays a match from <paramref name="setup"/>.</summary>
    /// <param name="rules">Rules config.</param>
    /// <param name="setup">Map, seed, and allowed races.</param>
    /// <param name="agents">An agent per seat.</param>
    /// <param name="observer">Called after every accepted command, e.g. to check invariants.</param>
    /// <param name="maxCommands">Commands allowed before giving up.</param>
    /// <param name="keepRecord">
    /// Whether to build the match record. Tournaments skip it: it holds every command and hashes every state.
    /// </param>
    /// <exception cref="InvalidOperationException">An agent submitted an illegal command, or the limit was hit.</exception>
    public static MatchResult Run(
        RulesConfig rules,
        MatchSetup setup,
        IReadOnlyDictionary<Seat, IPlayerAgent> agents,
        Action<GameState, ImmutableArray<GameEvent>>? observer = null,
        int maxCommands = DefaultMaxCommands,
        bool keepRecord = true)
    {
        GameState state = setup.CreateInitialState(rules);
        List<RecordedCommand> log = [];
        int commands = 0;
        while (GameEngine.PendingDecisions(state) is [Decision decision, ..])
        {
            if (commands >= maxCommands)
            {
                throw new InvalidOperationException($"Match exceeded {maxCommands} commands.");
            }

            ICommand command = agents[decision.Seat].DecideFor(state, decision.Seat);
            (state, ImmutableArray<GameEvent> events) = GameEngine.Apply(state, decision.Seat, command) switch
            {
                Accepted accepted => (accepted.State, accepted.Events),
                Rejected rejected => throw new InvalidOperationException(
                    $"{decision.Seat} submitted an illegal command {command}: {rejected.Violation.Message}"),
                _ => throw new InvalidOperationException("Unknown apply result."),
            };

            commands++;
            if (keepRecord)
            {
                log.Add(new RecordedCommand(commands, decision.Seat, command, StateHash.Compute(state), null));
            }

            observer?.Invoke(state, events);
        }

        MatchOutcome outcome = state.Outcome
            ?? throw new InvalidOperationException("No decisions pending but the match isn't over.");
        MatchRecord? record = keepRecord
            ? new(MatchRecord.CurrentFormatVersion, GameEngine.RulesVersion, rules.Hash, setup, [.. log])
            : null;
        return new MatchResult(outcome, state.Turn, state, record);
    }
}
