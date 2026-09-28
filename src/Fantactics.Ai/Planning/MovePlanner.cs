using Fantactics.Ai.Noise;
using Fantactics.Core;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.State;

namespace Fantactics.Ai.Planning;

/// <summary>
/// Gives the movement-phase orders. It builds one candidate per stance the style considers (see
/// <see cref="StanceTemplates"/>), resolves each against an opponent who holds, scores the result with the bot's
/// own weights plus the stance's bonus, and picks one. Later phases replace "the opponent holds" with sampled
/// opponent orders.
/// </summary>
public static class MovePlanner
{
    /// <summary>Builds the seat's orders from <paramref name="options"/>.</summary>
    public static SubmitMoveOrders Plan(PlanningContext context, MoveOptions options)
    {
        IReadOnlyList<(Stance Stance, double Bonus)> stances = StanceTemplates.For(context.Evaluator.Style);
        if (stances.Count == 0)
        {
            return CandidateGenerator.Build(context, options, context.Evaluator);
        }

        List<(Stance Stance, SubmitMoveOrders Orders, double Score)> candidates = [];
        foreach ((Stance stance, double bonus) in stances)
        {
            SubmitMoveOrders orders = CandidateGenerator.Build(
                context,
                options,
                context.Evaluator.WithStyle(StanceTemplates.Apply(stance, context.Evaluator.Style)));
            if (candidates.Count > 0 && !context.Budget.TryUse())
            {
                break;
            }

            candidates.Add((stance, orders, context.Evaluator.Evaluate(Resolve(context, orders)) + bonus));
        }

        double stanceTemperature = Math.Max(context.Profile.Skill.Temperature, context.Evaluator.Style.StanceTemperature);
        MistakeModel chooser = context.Evaluator.Style.StanceTemperature > 0
            ? new MistakeModel(context.Profile.Skill with { Temperature = stanceTemperature }, context.Random)
            : context.Mistakes;
        return candidates[chooser.Choose([.. candidates.Select(candidate => candidate.Score)])].Orders;
    }

    /// <summary>The state after the movement phase if the opponent holds every unit and deploys nothing.</summary>
    private static GameState Resolve(PlanningContext context, SubmitMoveOrders orders)
    {
        Seat seat = context.View.Seat;
        if (GameEngine.Apply(context.Belief, seat, orders) is not Accepted mine)
        {
            return context.Belief;
        }

        return GameEngine.Apply(mine.State, seat.Opponent(), SubmitMoveOrders.HoldAll) is Accepted both
            ? both.State
            : mine.State;
    }
}
