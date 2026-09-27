using Fantactics.Core.Commands;
using Fantactics.Core.Engine;

namespace Fantactics.Ai;

/// <summary>
/// A computer player (Simulation §4). It sees only its seat's <see cref="PlayerView"/> and the legal options, never
/// the full game state, so it can't cheat.
/// </summary>
public interface IPlayerAgent
{
    /// <summary>Chooses a command answering <paramref name="decision"/>.</summary>
    /// <param name="view">What the agent's seat can see.</param>
    /// <param name="decision">The pending decision.</param>
    /// <param name="legal">Legal options for the decision.</param>
    ICommand Decide(PlayerView view, Decision decision, LegalActions legal);
}
