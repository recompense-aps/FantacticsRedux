using Fantactics.Ai.Profiles;

namespace Fantactics.Ai.Noise;

/// <summary>
/// Turns scored options into a choice the way a player of the bot's skill would: the best one, a softmax pick
/// that favors good options, or now and then a blunder from the worse half. Every option offered is legal, so a
/// mistake is never an illegal move.
/// </summary>
/// <param name="skill">Temperature and blunder chance.</param>
/// <param name="random">The bot's seeded RNG.</param>
public sealed class MistakeModel(SkillSettings skill, Random random)
{
    /// <summary>Picks an index into <paramref name="scores"/>; ties go to the earliest option.</summary>
    /// <exception cref="ArgumentException"><paramref name="scores"/> is empty.</exception>
    public int Choose(IReadOnlyList<double> scores)
    {
        if (scores.Count == 0)
        {
            throw new ArgumentException("Nothing to choose from.", nameof(scores));
        }

        if (scores.Count == 1)
        {
            return 0;
        }

        if (skill.BlunderChance > 0 && random.NextDouble() < skill.BlunderChance)
        {
            List<int> worse = Enumerable.Range(0, scores.Count)
                .OrderBy(index => scores[index])
                .ThenBy(index => index)
                .Take(scores.Count / 2)
                .ToList();
            return worse[random.Next(worse.Count)];
        }

        double best = scores.Max();
        if (skill.Temperature <= 0)
        {
            return Enumerable.Range(0, scores.Count).First(index => scores[index] == best);
        }

        double[] weights = scores.Select(score => Math.Exp((score - best) / skill.Temperature)).ToArray();
        double pick = random.NextDouble() * weights.Sum();
        for (int index = 0; index < weights.Length; index++)
        {
            pick -= weights[index];
            if (pick < 0)
            {
                return index;
            }
        }

        return weights.Length - 1;
    }
}
