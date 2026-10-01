using System.Collections.Immutable;
using System.Globalization;
using Fantactics.Client.Logic.Menus;
using Fantactics.Core;
using Fantactics.Core.Rules;

namespace Fantactics.Client.Logic.Launch;

/// <summary>
/// Command-line options for jumping straight into a match (everything after <c>--</c> on the Godot command line):
/// <c>--new</c> with <c>--p1</c> to <c>--p4</c> seat labels, <c>--load &lt;file&gt;</c>,
/// <c>--saves &lt;dir&gt;</c>, and
/// <c>--autoplay</c> and <c>--drive</c> for the headless smoke runs. With none of them, the game opens its menu. Every option is
/// described in <c>notes/LaunchOptions.md</c>; keep it in step with <see cref="Parse"/>.
/// </summary>
/// <param name="NewMatch">Start a new match.</param>
/// <param name="P1">Who plays P1: <c>human</c>, <c>llm</c>, or <c>bot:&lt;spec&gt;</c>.</param>
/// <param name="P2">Who plays P2.</param>
/// <param name="P1Races">Races P1 may draft (<c>--p1-races Elves,Goblins</c>), or <c>null</c> for any.</param>
/// <param name="P2Races">Races P2 may draft, or <c>null</c> for any.</param>
/// <param name="Map">The map.</param>
/// <param name="Seed">Rules seed; random when not given.</param>
/// <param name="DraftAs">Bot profile that drafts and places for human seats (skip the draft), or <c>null</c> to draft by hand.</param>
/// <param name="Load">A match file to continue.</param>
/// <param name="As">The seat to show first.</param>
/// <param name="Out">Where to keep the match file (needed for LLM seats), or <c>null</c> for an autosave name.</param>
/// <param name="Saves">The saves folder, overriding the default.</param>
/// <param name="Autoplay">Play the match to the end with bots and quit (exit code 0 when it finishes).</param>
/// <param name="Speed">Animation speed multiplier (0 = instant).</param>
/// <param name="Debug">Open the debug panel at the start.</param>
/// <param name="Screenshot">Save a picture of the screen here a few seconds in, then quit (for checking layout).</param>
/// <param name="Menu">The menu screen to open: <c>main</c> (the default), <c>new</c>, <c>load</c>, or <c>settings</c>.</param>
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
    bool Debug,
    string Menu = "main")
{
    /// <summary>No options: open the menu.</summary>
    public static LaunchArgs None { get; } = new(
        false, "human", "bot:captain@easy", null, null, "riverford", null, null, null, null, null, null, false, null, null, false);

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

    /// <summary>Who plays P3 (<c>--p3</c>), or <c>null</c> for no third seat.</summary>
    public string? P3 { get; init; }

    /// <summary>Who plays P4 (<c>--p4</c>), or <c>null</c> for no fourth seat.</summary>
    public string? P4 { get; init; }

    /// <summary>Races P3 may draft (<c>--p3-races</c>), or <c>null</c> for any.</summary>
    public ImmutableSortedSet<string>? P3Races { get; init; }

    /// <summary>Races P4 may draft (<c>--p4-races</c>), or <c>null</c> for any.</summary>
    public ImmutableSortedSet<string>? P4Races { get; init; }

    /// <summary>
    /// Each seat's team in seat order (<c>--teams 1,2,1,2</c>), or <c>null</c> for everyone on a team of their own.
    /// </summary>
    public ImmutableArray<int>? Teams { get; init; }

    /// <summary>
    /// Play the human seats through synthetic clicks and key presses (<c>--drive</c>), starting from the menu unless
    /// a match option is given, and quit when the match ends (exit code 0).
    /// </summary>
    public bool Drive { get; init; }

    /// <summary>Where <c>--drive</c> saves screenshots at key points (<c>--shots &lt;dir&gt;</c>), or <c>null</c>.</summary>
    public string? Shots { get; init; }

    /// <summary>Whether this is a smoke run (<c>--autoplay</c> or <c>--drive</c>) that quits with an exit code.</summary>
    public bool IsSmokeRun => Autoplay || Drive;

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
                "--p3" => result with { P3 = Value(), NewMatch = true },
                "--p4" => result with { P4 = Value(), NewMatch = true },
                "--p3-races" => result with { P3Races = Races(Value()) },
                "--p4-races" => result with { P4Races = Races(Value()) },
                "--teams" => result with { Teams = TeamList(Value()) },
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
                "--drive" => result with { Drive = true },
                "--shots" => result with { Shots = Value() },
                "--speed" => result with { Speed = double.Parse(Value(), CultureInfo.InvariantCulture) },
                "--screenshot" => result with { Screenshot = Value() },
                "--debug" => result with { Debug = true },
                "--menu" => result with { Menu = MenuScreen(Value()) },
                _ => throw new ArgumentException($"Unknown option '{option}'."),
            };
        }

        if (result.Drive && result.Autoplay)
        {
            throw new ArgumentException("--drive and --autoplay don't go together: --drive plays human seats by input.");
        }

        if (result.Drive && result.Menu is not ("main" or "new"))
        {
            throw new ArgumentException("--drive starts from the main menu or --menu new.");
        }

        return result.Autoplay && result.Load is null && !result.NewMatch
            ? result with { NewMatch = true, P1 = "bot:captain@easy", P2 = "bot:captain@easy" }
            : result;
    }

    /// <summary>The new match these options describe, with the rules' budget and cap where none was given.</summary>
    /// <exception cref="ArgumentException">
    /// <c>--p4</c> is given without <c>--p3</c>, or the teams don't fit.
    /// </exception>
    public NewMatchForm Form(RulesConfig rules)
    {
        if (P4 is not null && P3 is null)
        {
            throw new ArgumentException("Give --p3 before --p4: seats are filled in order.");
        }

        List<KeyValuePair<Seat, SeatForm>> seats =
        [
            KeyValuePair.Create(Seat.P1, new SeatForm(
                P1, P1Races, P1Budget ?? Budget ?? rules.DraftBudget, P1StartingCap ?? StartingCap ?? rules.StartingCap)),
            KeyValuePair.Create(Seat.P2, new SeatForm(
                P2, P2Races, P2Budget ?? Budget ?? rules.DraftBudget, P2StartingCap ?? StartingCap ?? rules.StartingCap)),
        ];
        if (P3 is not null)
        {
            seats.Add(KeyValuePair.Create(Seat.P3, new SeatForm(
                P3, P3Races, Budget ?? rules.DraftBudget, StartingCap ?? rules.StartingCap)));
        }

        if (P4 is not null)
        {
            seats.Add(KeyValuePair.Create(Seat.P4, new SeatForm(
                P4, P4Races, Budget ?? rules.DraftBudget, StartingCap ?? rules.StartingCap)));
        }

        if (Teams is { } teams && teams.Length != seats.Count)
        {
            throw new ArgumentException($"--teams needs one team per seat ({seats.Count}).");
        }

        return new NewMatchForm(
            Map,
            ImmutableSortedDictionary.CreateRange(seats),
            Seed,
            DraftAs,
            Out,
            Teams is { } list
                ? seats.Zip(list, (seat, team) => KeyValuePair.Create(seat.Key, team)).ToImmutableSortedDictionary()
                : null);
    }

    /// <summary>A positive number of draft points.</summary>
    /// <exception cref="ArgumentException">The value isn't a positive whole number.</exception>
    private static int Points(string option, string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int points) && points > 0
            ? points
            : throw new ArgumentException($"{option} needs a positive whole number.");

    /// <summary>A comma-separated list of team numbers, one per seat in seat order.</summary>
    /// <exception cref="ArgumentException">A team isn't a positive whole number.</exception>
    private static ImmutableArray<int> TeamList(string value) =>
        [.. value
            .Split(',', StringSplitOptions.TrimEntries)
            .Select(team =>
                int.TryParse(team, NumberStyles.None, CultureInfo.InvariantCulture, out int number) && number > 0
                    ? number
                    : throw new ArgumentException("--teams needs positive whole numbers, e.g. 1,2,1,2."))];

    /// <summary>A menu screen name.</summary>
    /// <exception cref="ArgumentException">It isn't one of the menu screens.</exception>
    private static string MenuScreen(string value) => value is "main" or "new" or "load" or "settings"
        ? value
        : throw new ArgumentException("--menu is one of main, new, load, settings.");

    /// <summary>A comma-separated race list; <c>any</c> allows every race.</summary>
    private static ImmutableSortedSet<string>? Races(string value) =>
        value.Equals("any", StringComparison.OrdinalIgnoreCase)
            ? null
            : value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToImmutableSortedSet();
}
