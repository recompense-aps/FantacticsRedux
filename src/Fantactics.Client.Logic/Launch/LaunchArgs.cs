using System.Collections.Immutable;
using System.Globalization;
using Fantactics.Core;

namespace Fantactics.Client.Logic.Launch;

/// <summary>
/// Command-line options for jumping straight into a match (everything after <c>--</c> on the Godot command line):
/// <c>--new</c> with <c>--p1/--p2</c> seat labels, <c>--load &lt;file&gt;</c>, <c>--saves &lt;dir&gt;</c>, and
/// <c>--autoplay</c> for the headless smoke run. With none of them, the game opens its menu.
/// </summary>
/// <param name="NewMatch">Start a new match.</param>
/// <param name="P1">Who plays P1: <c>human</c>, <c>llm</c>, or <c>bot:&lt;spec&gt;</c>.</param>
/// <param name="P2">Who plays P2.</param>
/// <param name="P1Races">Races P1 may draft (<c>--p1-races Elves,Goblins</c>), or <c>null</c> for any.</param>
/// <param name="P2Races">Races P2 may draft, or <c>null</c> for any.</param>
/// <param name="Map">The map.</param>
/// <param name="Seed">Rules seed; random when not given.</param>
/// <param name="DraftAs">Bot profile that drafts and places for human seats (quick start), or <c>null</c>.</param>
/// <param name="Load">A match file to continue.</param>
/// <param name="As">The seat to show first.</param>
/// <param name="Out">Where to keep the match file (needed for LLM seats), or <c>null</c> for an autosave name.</param>
/// <param name="Saves">The saves folder, overriding the default.</param>
/// <param name="Autoplay">Play the match to the end with bots and quit (exit code 0 when it finishes).</param>
/// <param name="Speed">Animation speed multiplier (0 = instant).</param>
/// <param name="Debug">Open the debug panel at the start.</param>
/// <param name="Screenshot">Save a picture of the screen here a few seconds in, then quit (for checking layout).</param>
public sealed record LaunchArgs(
    bool NewMatch,
    string P1,
    string P2,
    ImmutableSortedSet<string>? P1Races,
    ImmutableSortedSet<string>? P2Races,
    string Map,
    ulong? Seed,
    string? DraftAs,
    string? Load,
    Seat? As,
    string? Out,
    string? Saves,
    bool Autoplay,
    double? Speed,
    string? Screenshot,
    bool Debug)
{
    /// <summary>No options: open the menu.</summary>
    public static LaunchArgs None { get; } = new(
        false, "human", "bot:captain@easy", null, null, "riverford", null, "captain", null, null, null, null, false, null, null, false);

    /// <summary>Draft budget for both seats (<c>--budget</c>), or <c>null</c> for the rules'.</summary>
    public int? Budget { get; init; }

    /// <summary>P1's draft budget (<c>--p1-budget</c>), overriding <see cref="Budget"/>.</summary>
    public int? P1Budget { get; init; }

    /// <summary>P2's draft budget (<c>--p2-budget</c>), overriding <see cref="Budget"/>.</summary>
    public int? P2Budget { get; init; }

    /// <summary>Starting cap for both seats (<c>--starting-cap</c>), or <c>null</c> for the rules'.</summary>
    public int? StartingCap { get; init; }

    /// <summary>P1's starting cap (<c>--p1-starting-cap</c>), overriding <see cref="StartingCap"/>.</summary>
    public int? P1StartingCap { get; init; }

    /// <summary>P2's starting cap (<c>--p2-starting-cap</c>), overriding <see cref="StartingCap"/>.</summary>
    public int? P2StartingCap { get; init; }

    /// <summary>Whether the options go straight into a match.</summary>
    public bool SkipsMenu => NewMatch || Load is not null || Autoplay;

    /// <summary>Parses the options.</summary>
    /// <exception cref="ArgumentException">An option is unknown or is missing its value.</exception>
    public static LaunchArgs Parse(IReadOnlyList<string> args)
    {
        LaunchArgs result = None;
        for (int i = 0; i < args.Count; i++)
        {
            string option = args[i];
            string Value() => i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[++i]
                : throw new ArgumentException($"{option} needs a value.");
            result = option switch
            {
                "--new" => result with { NewMatch = true },
                "--p1" => result with { P1 = Value(), NewMatch = true },
                "--p2" => result with { P2 = Value(), NewMatch = true },
                "--p1-races" => result with { P1Races = Races(Value()) },
                "--p2-races" => result with { P2Races = Races(Value()) },
                "--budget" => result with { Budget = Points(option, Value()) },
                "--p1-budget" => result with { P1Budget = Points(option, Value()) },
                "--p2-budget" => result with { P2Budget = Points(option, Value()) },
                "--starting-cap" => result with { StartingCap = Points(option, Value()) },
                "--p1-starting-cap" => result with { P1StartingCap = Points(option, Value()) },
                "--p2-starting-cap" => result with { P2StartingCap = Points(option, Value()) },
                "--map" => result with { Map = Value() },
                "--seed" => result with { Seed = ulong.Parse(Value(), CultureInfo.InvariantCulture) },
                "--draft-as" => result with { DraftAs = Value() is "none" ? null : args[i] },
                "--load" => result with { Load = Value() },
                "--as" => result with { As = Enum.Parse<Seat>(Value(), ignoreCase: true) },
                "--out" => result with { Out = Value() },
                "--saves" => result with { Saves = Value() },
                "--autoplay" => result with { Autoplay = true },
                "--speed" => result with { Speed = double.Parse(Value(), CultureInfo.InvariantCulture) },
                "--screenshot" => result with { Screenshot = Value() },
                "--debug" => result with { Debug = true },
                _ => throw new ArgumentException($"Unknown option '{option}'."),
            };
        }

        return result.Autoplay && result.Load is null && !result.NewMatch
            ? result with { NewMatch = true, P1 = "bot:captain@easy", P2 = "bot:captain@easy" }
            : result;
    }

    /// <summary>The match setup's allowed races: only the seats given a list; <c>null</c> when neither was.</summary>
    public ImmutableSortedDictionary<Seat, ImmutableSortedSet<string>>? AllowedRaces()
    {
        var bySeat = ImmutableSortedDictionary.CreateBuilder<Seat, ImmutableSortedSet<string>>();
        if (P1Races is not null)
        {
            bySeat[Seat.P1] = P1Races;
        }

        if (P2Races is not null)
        {
            bySeat[Seat.P2] = P2Races;
        }

        return bySeat.Count == 0 ? null : bySeat.ToImmutable();
    }

    /// <summary>The match setup's per-seat draft budgets; <c>null</c> when none was given.</summary>
    public ImmutableSortedDictionary<Seat, int>? DraftBudgets() => PerSeat(Budget, P1Budget, P2Budget);

    /// <summary>The match setup's per-seat starting caps; <c>null</c> when none was given.</summary>
    public ImmutableSortedDictionary<Seat, int>? StartingCaps() => PerSeat(StartingCap, P1StartingCap, P2StartingCap);

    private static ImmutableSortedDictionary<Seat, int>? PerSeat(int? both, int? p1, int? p2)
    {
        var bySeat = ImmutableSortedDictionary.CreateBuilder<Seat, int>();
        if ((p1 ?? both) is int first)
        {
            bySeat[Seat.P1] = first;
        }

        if ((p2 ?? both) is int second)
        {
            bySeat[Seat.P2] = second;
        }

        return bySeat.Count == 0 ? null : bySeat.ToImmutable();
    }

    /// <summary>A positive number of draft points.</summary>
    /// <exception cref="ArgumentException">The value isn't a positive whole number.</exception>
    private static int Points(string option, string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int points) && points > 0
            ? points
            : throw new ArgumentException($"{option} needs a positive whole number.");

    /// <summary>A comma-separated race list; <c>any</c> allows every race.</summary>
    private static ImmutableSortedSet<string>? Races(string value) =>
        value.Equals("any", StringComparison.OrdinalIgnoreCase)
            ? null
            : value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToImmutableSortedSet();
}
