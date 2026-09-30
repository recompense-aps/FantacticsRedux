using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Fantactics.Ai.Profiles;
using Fantactics.Core;
using Fantactics.Core.Maps;
using Fantactics.Core.Rules;
using Fantactics.Sim.Matches;
using Fantactics.Sim.Output;
using Fantactics.Sim.Tournaments;
using Fantactics.Sim.Views;
using McMaster.Extensions.CommandLineUtils;

namespace Fantactics.Sim.Commands;

/// <summary>Plays a bot-vs-bot tournament in memory and prints aggregate stats (Simulation §7, layer 6).</summary>
/// <param name="runner">Tournament runner.</param>
/// <param name="output">Where results are printed.</param>
[Command("run", Description = "Play a bot-vs-bot tournament and print stats.")]
public sealed class RunCommand(TournamentRunner runner, OutputWriter output) : DraftSetupCommand(output)
{
    /// <summary>P1's bot.</summary>
    [Option("--p1", Description = "P1's bot (default bot:random).")]
    public string P1 { get; set; } = "bot:random";

    /// <summary>P2's bot.</summary>
    [Option("--p2", Description = "P2's bot (default bot:random).")]
    public string P2 { get; set; } = "bot:random";

    /// <summary>Built-in map.</summary>
    [Option("--map", Description = "Built-in map (default riverford).")]
    public string Map { get; set; } = "riverford";

    /// <summary>Number of matches.</summary>
    [Option("--games", Description = "Number of matches (default 100).")]
    public int Games { get; set; } = 100;

    /// <summary>Seed of the first match.</summary>
    [Option("--seed", Description = "Seed of the first match; match i uses seed + i (default 1).")]
    public ulong Seed { get; set; } = 1;

    /// <summary>Play on all cores.</summary>
    [Option("--parallel", Description = "Play matches on every core (same as --threads 0).")]
    public bool Parallel { get; set; }

    /// <summary>Matches played at once.</summary>
    [Option("--threads", Description = "Matches played at once; 0 uses every core (default 1). Results don't change.")]
    public int? Threads { get; set; }

    /// <summary>A rules config file to play instead of the built-in rules.</summary>
    [Option("--rules", Description = "Rules config JSON to play instead of the built-in rules (for A/B tests).")]
    public string? Rules { get; set; }

    /// <summary>Per-match CSV output.</summary>
    [Option("--csv", Description = "Also write one CSV line per match to this file.")]
    public string? Csv { get; set; }

    /// <summary>Directory for detailed results.</summary>
    [Option("--out", Description = "Also write games.csv, units.csv, clashes.csv, and bots.csv to this directory.")]
    public string? Out { get; set; }

    /// <summary>A profile file for P1.</summary>
    [Option("--p1-profile", Description = "Play P1 with a bot profile JSON file instead of --p1.")]
    public string? P1Profile { get; set; }

    /// <summary>A profile file for P2.</summary>
    [Option("--p2-profile", Description = "Play P2 with a bot profile JSON file instead of --p2.")]
    public string? P2Profile { get; set; }

    /// <inheritdoc />
    protected override int Execute()
    {
        SeatKind p1 = SeatKind.Parse(P1);
        SeatKind p2 = SeatKind.Parse(P2);
        if (p1.BotName is not string p1Bot || p2.BotName is not string p2Bot)
        {
            throw new SimException("Tournaments need two bots, e.g. --p1 bot:captain --p2 bot:random.");
        }

        if (!MapLibrary.Names.Contains(Map) || Games < 1 || Threads < 0)
        {
            throw new SimException(
                $"Need a built-in map ({string.Join(", ", MapLibrary.Names)}), --games >= 1, and --threads >= 0.");
        }

        int threads = Threads ?? (Parallel ? 0 : 1);
        BotProfile? p1Profile = LoadProfile(P1Profile);
        BotProfile? p2Profile = LoadProfile(P2Profile);
        RulesConfig? variant = LoadVariant();
        ImmutableSortedDictionary<Seat, ImmutableSortedSet<string>>? allowed = AllowedRaces(variant ?? runner.Rules);
        TournamentOptions options = new(
            Map,
            allowed?.GetValueOrDefault(Seat.P1),
            allowed?.GetValueOrDefault(Seat.P2),
            p1Profile is null ? p1Bot : $"{p1Profile.Name}(file)",
            p2Profile is null ? p2Bot : $"{p2Profile.Name}(file)",
            Games,
            Seed,
            threads,
            p1Profile,
            p2Profile,
            DraftBudgets(),
            StartingCaps());
        (TournamentSummary summary, ImmutableArray<GameResult> games) = runner.Run(options, variant);
        if (Csv is string csv)
        {
            File.WriteAllLines(csv, [GameResult.CsvHeader, .. games.Select(game => game.ToCsv())]);
        }

        if (Out is string directory)
        {
            WriteDetails(directory, summary, games);
        }

        Output.Write(summary, Format);
        return ExitCodes.Ok;
    }

    /// <summary>Writes the bulk data the summary leaves out, for follow-up analysis.</summary>
    private static void WriteDetails(string directory, TournamentSummary summary, ImmutableArray<GameResult> games)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllLines(
            Path.Combine(directory, "games.csv"),
            [GameResult.CsvHeader, .. games.Select(game => game.ToCsv())]);
        File.WriteAllLines(
            Path.Combine(directory, "units.csv"),
            [
                "seat,type,fielded,damage,kills,deaths,picked,picked_wins",
                .. summary.UnitStats.Select(u =>
                    $"{u.Seat},{u.Type},{u.Fielded},{u.Damage},{u.Kills},{u.Deaths},{u.Picked},{u.PickedWins}"),
            ]);
        File.WriteAllLines(
            Path.Combine(directory, "clashes.csv"),
            [
                "type_a,type_b,clashes,a_wins,b_wins,unresolved,a_win_rate",
                .. summary.Clashes.Select(c =>
                    $"{c.TypeA},{c.TypeB},{c.Clashes},{c.AWins},{c.BWins},{c.Unresolved},"
                    + c.AWinRate.ToString(CultureInfo.InvariantCulture)),
            ]);
        File.WriteAllLines(
            Path.Combine(directory, "bots.csv"),
            [
                "seat,bot,first_contact,no_contact,first_arrival,objective,damage,damage_taken,destroyed",
                .. summary.Fingerprints.Select(f => string.Join(
                    ',',
                    [
                        f.Seat,
                        f.Bot,
                        .. new[]
                            {
                                f.FirstContact, f.NoContact, f.FirstArrival, f.Objective, f.Damage, f.DamageTaken,
                                f.Destroyed,
                            }
                            .Select(value => value.ToString(CultureInfo.InvariantCulture)),
                    ])),
            ]);
    }

    private static BotProfile? LoadProfile(string? path)
    {
        if (path is null)
        {
            return null;
        }

        try
        {
            return BotLibrary.FromJson(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or JsonException)
        {
            throw new SimException($"Can't load bot profile from '{path}': {ex.Message}");
        }
    }

    private RulesConfig? LoadVariant()
    {
        if (Rules is not string path)
        {
            return null;
        }

        try
        {
            return RulesConfig.FromJson(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            throw new SimException($"Can't load rules from '{path}': {ex.Message}");
        }
    }
}
