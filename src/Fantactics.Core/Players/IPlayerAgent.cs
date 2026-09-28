using Fantactics.Core.Commands;
using Fantactics.Core.Engine;

namespace Fantactics.Core.Players;

/// <summary>
/// A computer player (Simulation §4). It sees only its seat's <see cref="PlayerView"/> and the legal options, never
/// the full game state, so it can't cheat. Both use the seat's own unit ids (<see cref="ViewIds"/>); call it through
/// <see cref="PlayerAgentExtensions.DecideFor"/>, which translates the answer back to engine ids.
/// </summary>
public interface IPlayerAgent
{
    /// <summary>Chooses a command answering <paramref name="decision"/>.</summary>
    /// <param name="view">What the agent's seat can see.</param>
    /// <param name="decision">The pending decision.</param>
    /// <param name="legal">Legal options for the decision.</param>
    ICommand Decide(PlayerView view, Decision decision, LegalActions legal);
}
