using System.Collections.Immutable;
using System.Text.Json;
using Fantactics.Core.Maps;
using Fantactics.Core.Rules;
using Fantactics.Sim.Matches;
using Fantactics.Sim.Output;
using Fantactics.Sim.Tournaments;
using McMaster.Extensions.CommandLineUtils;

namespace Fantactics.Sim.Commands;

/// <summary>Plays a bot-vs-bot tournament in memory and prints aggregate stats (Simulation §7, layer 6).</summary>
/// <param name="runner">Tournament runner.</param>
/// <param name="output">Where results are printed.</param>
[Command("run", Description = "Play a bot-vs-bot tournament and print stats.")]
public sealed class RunCommand(TournamentRunner runner, OutputWriter output) : SimCommand(output)
{
    /// <summary>P1's bot.</summary>
    [Option("--p1", Description = "P1's bot (default bot:random).")]
    public string P1 { get; set; } = "bot:random";

    /// <summary>P2's bot.</summary>
    [Option("--p2", Description = "P2's bot (default bot:random).")]
    public string P2 { get; set; } = "bot:random";

    /// <summary>P1's race.</summary>
    [Option("--p1-race", Description = "P1's race (default Elves).")]
    public string P1Race { get; set; } = "Elves";

    /// <summary>P2's race.</summary>
    [Option("--p2-race", Description = "P2's race (default Goblins).")]
    public string P2Race { get; set; } = "Goblins";

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
    [Option("--parallel", Description = "Play matches in parallel.")]
    public bool Parallel { get; set; }

    /// <summary>A rules config file to play instead of the built-in rules.</summary>
    [Option("--rules", Description = "Rules config JSON to play instead of the built-in rules (for A/B tests).")]
    public string? Rules { get; set; }

    /// <summary>Per-match CSV output.</summary>
    [Option("--csv", Description = "Also write one CSV line per match to this file.")]
    public string? Csv { get; set; }

    /// <inheritdoc />
    protected override int Execute()
    {
        SeatKind p1 = SeatKind.Parse(P1);
        SeatKind p2 = SeatKind.Parse(P2);
        if (p1.BotName is not string p1Bot || p2.BotName is not string p2Bot)
        {
            throw new SimException("Tournaments need two bots, e.g. --p1 bot:random --p2 bot:random.");
        }

        if (!MapLibrary.Names.Contains(Map) || Games < 1)
        {
            throw new SimException($"Need a built-in map ({string.Join(", ", MapLibrary.Names)}) and --games >= 1.");
        }

        (TournamentSummary summary, ImmutableArray<GameResult> games) = runner.Run(
            new TournamentOptions(Map, P1Race, P2Race, p1Bot, p2Bot, Games, Seed, Parallel),
            LoadVariant());
        if (Csv is string csv)
        {
            File.WriteAllLines(csv, [GameResult.CsvHeader, .. games.Select(game => game.ToCsv())]);
        }

        Output.Write(summary, Format);
        return ExitCodes.Ok;
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
