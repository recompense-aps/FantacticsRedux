using System.Collections.Immutable;
using Fantactics.Core;
using Fantactics.Core.Engine;
using Fantactics.Core.Rules;

namespace Fantactics.Ai.Belief;

/// <summary>
/// Guesses the opponent's hidden reserve: a composition of their race's unit types whose Cost adds up to the reserve
/// value the view shows (GameDesign §4.4). Only the value matters for Rout, so a plausible guess is enough for now.
/// </summary>
public static class ReserveGuesser
{
    /// <summary>Unit types that make up the enemy reserve value, fewest units first; empty if nothing fits.</summary>
    /// <param name="view">The guessing seat's view.</param>
    /// <param name="rules">Rules in effect.</param>
    public static ImmutableArray<string> Guess(PlayerView view, RulesConfig rules)
    {
        PlayerSummary enemy = view.Players[view.Seat.Opponent()];
        if (enemy.ReserveValue <= 0)
        {
            return [];
        }

        // Uniques can't be proven absent from the field, so leave them out; the cheap commons always fit.
        List<(string Type, int Cost)> types = rules.Units
            .Where(pair => pair.Value.Race == enemy.Race && !pair.Value.Unique)
            .Select(pair => (pair.Key, pair.Value.Cost))
            .OrderByDescending(type => type.Cost)
            .ThenBy(type => type.Key, StringComparer.Ordinal)
            .ToList();
        return FewestUnits(types, enemy.ReserveValue);
    }

    /// <summary>Coin-change DP: the fewest types whose costs sum exactly to <paramref name="value"/>.</summary>
    private static ImmutableArray<string> FewestUnits(List<(string Type, int Cost)> types, int value)
    {
        int[] count = Enumerable.Repeat(int.MaxValue, value + 1).ToArray();
        int[] pick = new int[value + 1];
        count[0] = 0;
        for (int total = 1; total <= value; total++)
        {
            for (int i = 0; i < types.Count; i++)
            {
                int rest = total - types[i].Cost;
                if (rest >= 0 && count[rest] != int.MaxValue && count[rest] + 1 < count[total])
                {
                    count[total] = count[rest] + 1;
                    pick[total] = i;
                }
            }
        }

        if (count[value] == int.MaxValue)
        {
            return [];
        }

        List<string> guess = [];
        for (int total = value; total > 0; total -= types[pick[total]].Cost)
        {
            guess.Add(types[pick[total]].Type);
        }

        return [.. guess];
    }
}
