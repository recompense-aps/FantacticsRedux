using Fantactics.Ai.Belief;
using Fantactics.Ai.Evaluation;
using Fantactics.Ai.Noise;
using Fantactics.Ai.Planning;
using Fantactics.Ai.Profiles;
using Fantactics.Core;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Players;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Ai;

/// <summary>
/// The configurable bot. It rebuilds a belief state from its view (never the real state), then hands each decision
/// to a planner that scores options with an <see cref="Evaluator"/> weighted by the profile's style, and picks one
/// at the profile's skill.
/// </summary>
/// <param name="profile">Difficulty and style.</param>
/// <param name="rules">Rules the match uses.</param>
/// <param name="seed">Seed for the bot's mistakes and tie-breaking.</param>
public sealed class TacticalAgent(BotProfile profile, RulesConfig rules, int seed) : IPlayerAgent
{
    private readonly Random _random = new(seed);

    /// <summary>The bot's profile.</summary>
    public BotProfile Profile => profile;

    /// <inheritdoc />
    public ICommand Decide(PlayerView view, Decision decision, LegalActions legal)
    {
        GameState belief = BeliefState.From(view, rules, ReserveGuesser.GuessAll(view, rules));
        bool onField = view.Phase is Phase.Movement or Phase.Action;
        ThreatMap threats = onField && !profile.Skill.IgnoreThreats
            ? ThreatMap.ForEnemiesOf(belief, view.Seat)
            : ThreatMap.Empty;
        PlanningContext context = new(
            view,
            belief,
            new Evaluator(view.Seat, profile.Style, profile.Skill, threats),
            new MistakeModel(profile.Skill, _random),
            new SimulationBudget(profile.Skill.Budget),
            profile,
            _random);

        return decision switch
        {
            DraftArmyDecision => DraftPlanner.Plan(context, legal.Draft ?? throw MissingOptions(decision)),
            PlaceStartingArmyDecision =>
                PlacementPlanner.Plan(context, legal.Placement ?? throw MissingOptions(decision)),
            SubmitMoveOrdersDecision => MovePlanner.Plan(context, legal.Moves ?? throw MissingOptions(decision)),
            ChooseUnitActionDecision => ActionPlanner.Choose(context, legal.Actions),
            _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unknown decision."),
        };
    }

    private static InvalidOperationException MissingOptions(Decision decision) =>
        new($"No legal options supplied for {decision}.");
}
