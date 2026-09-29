using System.Collections.Immutable;
using Fantactics.Core;
using Fantactics.Core.Rules;
using Fantactics.Sim.Matches;
using Fantactics.Sim.Output;
using Fantactics.Sim.Views;
using McMaster.Extensions.CommandLineUtils;

namespace Fantactics.Sim.Commands;

/// <summary>
/// Base for commands that set up matches: which races each seat may draft, and per-seat draft budgets and starting
/// caps overriding the rules' (GameDesign §4.4).
/// </summary>
/// <param name="output">Where results are printed.</param>
public abstract class DraftSetupCommand(OutputWriter output) : SimCommand(output)
{
    /// <summary>Races P1 may draft.</summary>
    [Option("--p1-races", Description = "Races P1 may draft, comma-separated, e.g. Elves,Goblins (default any).")]
    public string? P1Races { get; set; }

    /// <summary>Races P2 may draft.</summary>
    [Option("--p2-races", Description = "Races P2 may draft, comma-separated (default any).")]
    public string? P2Races { get; set; }

    /// <summary>Draft budget for both seats.</summary>
    [Option("--budget", Description = "Draft budget for both seats (default: the rules', 40).")]
    public int? Budget { get; set; }

    /// <summary>P1's draft budget.</summary>
    [Option("--p1-budget", Description = "P1's draft budget, overriding --budget.")]
    public int? P1Budget { get; set; }

    /// <summary>P2's draft budget.</summary>
    [Option("--p2-budget", Description = "P2's draft budget, overriding --budget.")]
    public int? P2Budget { get; set; }

    /// <summary>Starting cap for both seats.</summary>
    [Option("--starting-cap", Description = "Most Cost each seat may place at the start (default: the rules', 30).")]
    public int? StartingCap { get; set; }

    /// <summary>P1's starting cap.</summary>
    [Option("--p1-starting-cap", Description = "P1's starting cap, overriding --starting-cap.")]
    public int? P1StartingCap { get; set; }

    /// <summary>P2's starting cap.</summary>
    [Option("--p2-starting-cap", Description = "P2's starting cap, overriding --starting-cap.")]
    public int? P2StartingCap { get; set; }

    /// <summary>The races each seat may draft, or <c>null</c> for any.</summary>
    /// <exception cref="SimException">A race is unknown.</exception>
    protected ImmutableSortedDictionary<Seat, ImmutableSortedSet<string>>? AllowedRaces(RulesConfig rules) =>
        RaceText.ParseAllowed(P1Races, P2Races, rules.Races.Keys);

    /// <summary>Per-seat draft budgets, or <c>null</c> to use the rules'.</summary>
    /// <exception cref="SimException">A budget isn't positive.</exception>
    protected ImmutableSortedDictionary<Seat, int>? DraftBudgets() =>
        SeatValues.Combine(Budget, P1Budget, P2Budget, "Draft budgets");

    /// <summary>Per-seat starting caps, or <c>null</c> to use the rules'.</summary>
    /// <exception cref="SimException">A cap isn't positive.</exception>
    protected ImmutableSortedDictionary<Seat, int>? StartingCaps() =>
        SeatValues.Combine(StartingCap, P1StartingCap, P2StartingCap, "Starting caps");
}
