using Fantactics.Ai.Noise;
using Fantactics.Ai.Profiles;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Rules;

namespace Fantactics.Ai.Planning;

/// <summary>
/// Drafts an army (GameDesign §4.4): picks unit types by a rough power-per-cost rating times the style's
/// <see cref="StyleWeights.DraftBias"/>, with diminishing returns for repeats so armies stay mixed. A race focus
/// (<see cref="StyleWeights.RaceFocus"/>) pulls later picks toward the races already drafted. Eager styles
/// fill the starting cap; patient ones (reserve eagerness below 0.5) start lighter and keep a bigger reserve.
/// </summary>
public static class DraftPlanner
{
    private const double RepeatFalloff = 0.8;
    private const double AbilityBonus = 1.0;
    private const int PatientHoldback = 6;

    /// <summary>Builds a legal draft from <paramref name="options"/>.</summary>
    public static SubmitDraft Plan(PlanningContext context, DraftOptions options)
    {
        RulesConfig rules = context.Belief.Rules;
        StyleWeights style = context.Evaluator.Style;
        MistakeModel chooser = style.DraftTemperature > 0
            ? new MistakeModel(context.Profile.Skill with { Temperature = style.DraftTemperature }, context.Random)
            : context.Mistakes;
        int startingTarget = style.ReserveEagerness < 0.5
            ? options.StartingCap - PatientHoldback
            : options.StartingCap;

        List<string> starting = [];
        List<string> reserve = [];
        Dictionary<string, int> copies = [];
        Dictionary<string, int> races = [];
        int spent = 0;
        int startingCost = 0;
        while (true)
        {
            List<DraftUnitOption> affordable = options.Units
                .Where(unit => spent + unit.Cost <= options.Budget && !(unit.Unique && copies.ContainsKey(unit.Type)))
                .OrderBy(unit => unit.Type, StringComparer.Ordinal)
                .ToList();
            if (affordable.Count == 0)
            {
                break;
            }

            int picked = starting.Count + reserve.Count;
            List<double> scores = affordable
                .Select(unit => Power(rules.Units[unit.Type])
                    * style.DraftBias.GetValueOrDefault(unit.Type, 1.0)
                    * Math.Pow(RepeatFalloff, copies.GetValueOrDefault(unit.Type))
                    * (1 + style.RaceFocus * RaceShare(unit.Race, picked, races)))
                .ToList();
            DraftUnitOption pick = affordable[chooser.Choose(scores)];
            bool toStarting = starting.Count == 0 || startingCost + pick.Cost <= startingTarget;
            toStarting &= startingCost + pick.Cost <= options.StartingCap;
            (toStarting ? starting : reserve).Add(pick.Type);
            startingCost += toStarting ? pick.Cost : 0;
            spent += pick.Cost;
            copies[pick.Type] = copies.GetValueOrDefault(pick.Type) + 1;
            races[pick.Race] = races.GetValueOrDefault(pick.Race) + 1;
        }

        return new SubmitDraft([.. starting], [.. reserve]);
    }

    /// <summary>The share of the <paramref name="picked"/> units so far that are of <paramref name="race"/>.</summary>
    private static double RaceShare(string race, int picked, Dictionary<string, int> races) =>
        picked == 0 ? 0 : (double)races.GetValueOrDefault(race) / picked;

    /// <summary>Durability times damage, with a little credit for mobility and abilities, per point of Cost.</summary>
    private static double Power(UnitDefinition unit)
    {
        double durability = unit.Hp + 2 * unit.Defense;
        double damage = unit.Attack + (unit.MaxRange - 1);
        double mobility = 1 + unit.Movement / 10.0;
        return durability * damage * mobility / unit.Cost + AbilityBonus * unit.Abilities.Length;
    }
}
