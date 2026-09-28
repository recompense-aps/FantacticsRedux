using Fantactics.Ai.Evaluation;
using Fantactics.Ai.Noise;
using Fantactics.Ai.Profiles;
using Fantactics.Core.Engine;
using Fantactics.Core.State;

namespace Fantactics.Ai.Planning;

/// <summary>Everything a planner needs for one decision.</summary>
/// <param name="View">What the seat sees.</param>
/// <param name="Belief">The view rebuilt as a state, with hidden parts guessed.</param>
/// <param name="Evaluator">Scores states and placements for the seat.</param>
/// <param name="Mistakes">Picks among scored options at the bot's skill.</param>
/// <param name="Budget">Simulations left for this decision.</param>
/// <param name="Profile">The bot's profile.</param>
/// <param name="Random">The bot's seeded RNG, for planners that need their own <see cref="MistakeModel"/>.</param>
public sealed record PlanningContext(
    PlayerView View,
    GameState Belief,
    Evaluator Evaluator,
    MistakeModel Mistakes,
    SimulationBudget Budget,
    BotProfile Profile,
    Random Random);
