using System.Collections.Immutable;
using Fantactics.Core;
using Fantactics.Core.Engine;
using Fantactics.Core.Rules;

namespace Fantactics.Ai.Belief;

/// <summary>
/// Guesses other seats' hidden reserves: a composition of unit types from the races they drafted (the draft reveal,
/// GameDesign §4.4) whose Cost adds up to the reserve value the view shows. Only the value matters for Rout, so a
/// plausible guess is enough for now.
/// </summary>
public static class ReserveGuesser
{
    /// <summary>
    /// Unit types that make up the only opponent's reserve value, fewest units first; empty if nothing fits.
    /// </summary>
    /// <param name="view">The guessing seat's view.</param>
    /// <param name="rules">Rules in effect.</param>
    /// <exception cref="InvalidOperationException">The seat doesn't have exactly one opponent.</exception>
    public static ImmutableArray<string> Guess(PlayerView view, RulesConfig rules) =>
        Guess(view, rules, view.SoleOpponent());

    /// <summary>The guessed reserve of every other seat still playing, teammates included.</summary>
    /// <param name="view">The guessing seat's view.</param>
    /// <param name="rules">Rules in effect.</param>
    public static IReadOnlyDictionary<Seat, IReadOnlyList<string>> GuessAll(PlayerView view, RulesConfig rules) =>
        view.Players.Values
            .Where(player => player.Seat != view.Seat && !player.Eliminated)
            .ToDictionary(player => player.Seat, player => (IReadOnlyList<string>)Guess(view, rules, player.Seat));

    /// <summary>Unit types that make up <paramref name="other"/>'s reserve value, fewest units first.</summary>
    /// <param name="view">The guessing seat's view.</param>
    /// <param name="rules">Rules in effect.</param>
    /// <param name="other">The seat whose reserve to guess.</param>
    public static ImmutableArray<string> Guess(PlayerView view, RulesConfig rules, Seat other)
    {
        PlayerSummary enemy = view.Players[other];
        if (enemy.ReserveValue <= 0)
        {
            return [];
        }

        // Uniques can't be proven absent from the field, so leave them out; the cheap commons always fit. Among equal
        // costs, the race they drafted most goes first, so the guess leans on the army's main race.
        IReadOnlyDictionary<string, int> drafted = enemy.DraftedRaces
            ?? (enemy.AllowedRaces ?? [.. rules.Races.Keys]).ToImmutableSortedDictionary(race => race, _ => 1);
        List<(string Type, int Cost)> types = rules.Units
            .Where(pair => drafted.ContainsKey(pair.Value.Race) && !pair.Value.Unique)
            .OrderByDescending(pair => pair.Value.Cost)
            .ThenByDescending(pair => drafted[pair.Value.Race])
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => (pair.Key, pair.Value.Cost))
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
