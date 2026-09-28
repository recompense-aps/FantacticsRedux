using System.Collections.Immutable;
using Fantactics.Ai.Evaluation;
using Fantactics.Ai.Profiles;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.State;

namespace Fantactics.Ai.Planning;

/// <summary>
/// Chooses a unit's action (GameDesign §4.2) by applying each legal option to the belief state and scoring the
/// result, plus the style's taste for attacking (<see cref="StyleWeights.Bloodlust"/>) and for finishing off
/// wounded enemies (<see cref="StyleWeights.FocusFire"/>). One ply: it doesn't yet look at the slots still to
/// come, so Delay is never chosen.
/// </summary>
public static class ActionPlanner
{
    /// <summary>Picks one of <paramref name="options"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="options"/> has no Wait.</exception>
    public static ICommand Choose(PlanningContext context, ImmutableArray<ActionOption> options)
    {
        ICommand wait = options.FirstOrDefault(option => option.Command is Wait)?.Command
            ?? throw new ArgumentException("Every action decision offers Wait.", nameof(options));

        // Most promising first, so a spent budget cuts the least likely options. Wait sits before abilities: an
        // ability that scores no better than waiting isn't worth using.
        List<ActionOption> ordered = options
            .Where(option => option.Command is not Delay)
            .OrderBy(option => option.Command switch
            {
                Attack => 0,
                Wait => 1,
                _ => 2,
            })
            .ThenByDescending(option => option.Preview?.Kills ?? false)
            .ThenByDescending(option => option.Preview?.Damage ?? 0)
            .ToList();

        List<ICommand> scored = [];
        List<double> scores = [];
        foreach (ActionOption option in ordered)
        {
            if (option.Command is not Wait && !context.Budget.TryUse())
            {
                continue;
            }

            if (GameEngine.Apply(context.Belief, context.View.Seat, option.Command) is Accepted accepted)
            {
                scored.Add(option.Command);
                scores.Add(context.Evaluator.Evaluate(accepted.State) + Taste(context, option));
            }
        }

        return scored.Count == 0 ? wait : scored[context.Mistakes.Choose(scores)];
    }

    /// <summary>The style's bonus for an option that strikes an enemy.</summary>
    private static double Taste(PlanningContext context, ActionOption option)
    {
        StyleWeights style = context.Evaluator.Style;
        if (option.Preview is not AttackPreview preview
            || !context.Belief.Units.TryGetValue(preview.TargetId, out Unit? target))
        {
            return 0;
        }

        double missing = 1 - (double)target.Hp / context.Belief.DefinitionOf(target).Hp;
        return style.Bloodlust + style.FocusFire * missing * Evaluator.ValueOf(context.Belief, target);
    }
}
