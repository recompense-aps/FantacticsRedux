using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.State;

namespace Fantactics.Core.Players;

/// <summary>Runs an <see cref="IPlayerAgent"/> against a real state without leaking engine ids to it.</summary>
public static class PlayerAgentExtensions
{
    /// <summary>
    /// Asks <paramref name="agent"/> for <paramref name="seat"/>'s pending decision: it gets the seat's view and legal
    /// options, and its answer comes back with engine ids, ready for <see cref="GameEngine.Apply"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException"><paramref name="seat"/> owes no decision.</exception>
    public static ICommand DecideFor(this IPlayerAgent agent, GameState state, Seat seat)
    {
        LegalActions legal = LegalActions.ForView(state, seat)
            ?? throw new InvalidOperationException($"{seat} owes no decision.");
        ICommand command = agent.Decide(PlayerView.Project(state, seat), legal.Decision, legal);
        return ViewIds.For(state, seat).ToEngine(command);
    }
}
