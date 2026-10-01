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

    /// <summary>Races P3 may draft.</summary>
    [Option("--p3-races", Description = "Races P3 may draft, comma-separated (default any).")]
    public string? P3Races { get; set; }

    /// <summary>Races P4 may draft.</summary>
    [Option("--p4-races", Description = "Races P4 may draft, comma-separated (default any).")]
    public string? P4Races { get; set; }

    /// <summary>Draft budget for every seat.</summary>
    [Option("--budget", Description = "Draft budget for every seat (default: the rules', 40).")]
    public int? Budget { get; set; }

    /// <summary>P1's draft budget.</summary>
    [Option("--p1-budget", Description = "P1's draft budget, overriding --budget.")]
    public int? P1Budget { get; set; }

    /// <summary>P2's draft budget.</summary>
    [Option("--p2-budget", Description = "P2's draft budget, overriding --budget.")]
    public int? P2Budget { get; set; }

    /// <summary>P3's draft budget.</summary>
    [Option("--p3-budget", Description = "P3's draft budget, overriding --budget.")]
    public int? P3Budget { get; set; }

    /// <summary>P4's draft budget.</summary>
    [Option("--p4-budget", Description = "P4's draft budget, overriding --budget.")]
    public int? P4Budget { get; set; }

    /// <summary>Starting cap for every seat.</summary>
    [Option("--starting-cap", Description = "Most Cost each seat may place at the start (default: the rules', 30).")]
    public int? StartingCap { get; set; }

    /// <summary>P1's starting cap.</summary>
    [Option("--p1-starting-cap", Description = "P1's starting cap, overriding --starting-cap.")]
    public int? P1StartingCap { get; set; }

    /// <summary>P2's starting cap.</summary>
    [Option("--p2-starting-cap", Description = "P2's starting cap, overriding --starting-cap.")]
    public int? P2StartingCap { get; set; }

    /// <summary>P3's starting cap.</summary>
    [Option("--p3-starting-cap", Description = "P3's starting cap, overriding --starting-cap.")]
    public int? P3StartingCap { get; set; }

    /// <summary>P4's starting cap.</summary>
    [Option("--p4-starting-cap", Description = "P4's starting cap, overriding --starting-cap.")]
    public int? P4StartingCap { get; set; }

    /// <summary>The races each of <paramref name="seats"/> may draft, or <c>null</c> for any.</summary>
    /// <exception cref="SimException">
    /// A race is unknown, or races are given for a seat the match doesn't have.
    /// </exception>
    protected ImmutableSortedDictionary<Seat, ImmutableSortedSet<string>>? AllowedRaces(
        RulesConfig rules,
        IReadOnlyCollection<Seat>? seats = null)
    {
        Dictionary<Seat, string?> lists = new()
        {
            [Seat.P1] = P1Races,
            [Seat.P2] = P2Races,
            [Seat.P3] = P3Races,
            [Seat.P4] = P4Races,
        };
        return RaceText.ParseAllowed(Only(lists, seats ?? SeatExtensions.TwoPlayer, "races"), rules.Races.Keys);
    }

    /// <summary>Per-seat draft budgets for <paramref name="seats"/>, or <c>null</c> to use the rules'.</summary>
    /// <exception cref="SimException">
    /// A budget isn't positive, or is given for a seat the match doesn't have.
    /// </exception>
    protected ImmutableSortedDictionary<Seat, int>? DraftBudgets(IReadOnlyCollection<Seat>? seats = null) =>
        Combine(Budget, [P1Budget, P2Budget, P3Budget, P4Budget], seats, "budget", "Draft budgets");

    /// <summary>Per-seat starting caps for <paramref name="seats"/>, or <c>null</c> to use the rules'.</summary>
    /// <exception cref="SimException">
    /// A cap isn't positive, or is given for a seat the match doesn't have.
    /// </exception>
    protected ImmutableSortedDictionary<Seat, int>? StartingCaps(IReadOnlyCollection<Seat>? seats = null) =>
        Combine(
            StartingCap,
            [P1StartingCap, P2StartingCap, P3StartingCap, P4StartingCap],
            seats,
            "starting-cap",
            "Starting caps");

    private static ImmutableSortedDictionary<Seat, int>? Combine(
        int? all,
        int?[] bySeatNumber,
        IReadOnlyCollection<Seat>? seats,
        string option,
        string name)
    {
        Dictionary<Seat, int?> perSeat = Enum.GetValues<Seat>()
            .ToDictionary(seat => seat, seat => bySeatNumber[(int)seat]);
        IReadOnlyCollection<Seat> playing = seats ?? SeatExtensions.TwoPlayer;
        return SeatValues.Combine(all, Only(perSeat, playing, option), playing, name);
    }

    /// <summary>The values given for <paramref name="seats"/>; a value for any other seat is an error.</summary>
    private static Dictionary<Seat, T?> Only<T>(
        Dictionary<Seat, T?> values,
        IReadOnlyCollection<Seat> seats,
        string option)
    {
        Seat? stray = values.Keys
            .Where(seat => values[seat] is not null && !seats.Contains(seat))
            .Cast<Seat?>()
            .FirstOrDefault();
        string Name(Seat seat) => seat.ToString().ToLowerInvariant();
        return stray is Seat seat
            ? throw new SimException($"--{Name(seat)}-{option} is set, but {seat} isn't playing.")
            : values.Where(pair => seats.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);
    }
}
