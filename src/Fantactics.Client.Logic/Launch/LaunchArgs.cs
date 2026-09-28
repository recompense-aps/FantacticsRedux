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
/// <param name="P1Race">P1's race.</param>
/// <param name="P2Race">P2's race.</param>
/// <param name="Map">The map.</param>
/// <param name="Seed">Rules seed; random when not given.</param>
/// <param name="DraftAs">Bot profile that drafts and places for human seats (quick start), or <c>null</c>.</param>
/// <param name="Load">A match file to continue.</param>
/// <param name="As">The seat to show first.</param>
/// <param name="Out">Where to keep the match file (needed for LLM seats), or <c>null</c> for an autosave name.</param>
/// <param name="Saves">The saves folder, overriding the default.</param>
/// <param name="Autoplay">Play the match to the end with bots and quit (exit code 0 when it finishes).</param>
/// <param name="Speed">Animation speed multiplier (0 = instant).</param>
/// <param name="Screenshot">Save a picture of the screen here a few seconds in, then quit (for checking layout).</param>
public sealed record LaunchArgs(
    bool NewMatch,
    string P1,
    string P2,
    string P1Race,
    string P2Race,
    string Map,
    ulong? Seed,
    string? DraftAs,
    string? Load,
    Seat? As,
    string? Out,
    string? Saves,
    bool Autoplay,
    double? Speed,
    string? Screenshot)
{
    /// <summary>No options: open the menu.</summary>
    public static LaunchArgs None { get; } = new(
        false, "human", "bot:captain@easy", "Elves", "Goblins", "riverford", null, "captain", null, null, null, null, false, null, null);

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
                "--p1-race" => result with { P1Race = Value() },
                "--p2-race" => result with { P2Race = Value() },
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
                _ => throw new ArgumentException($"Unknown option '{option}'."),
            };
        }

        return result.Autoplay && result.Load is null && !result.NewMatch
            ? result with { NewMatch = true, P1 = "bot:captain@easy", P2 = "bot:captain@easy" }
            : result;
    }
}
