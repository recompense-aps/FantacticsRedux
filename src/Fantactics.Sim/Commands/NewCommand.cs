using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using Fantactics.Core;
using Fantactics.Core.Maps;
using Fantactics.Core.Records;
using Fantactics.Sim.Matches;
using Fantactics.Sim.Output;
using Fantactics.Sim.Views;
using McMaster.Extensions.CommandLineUtils;

namespace Fantactics.Sim.Commands;

/// <summary>Creates a match file. Bot seats play their opening decisions right away.</summary>
/// <param name="store">Match file store.</param>
/// <param name="output">Where results are printed.</param>
[Command("new", Description = "Create a match file.")]
public sealed class NewCommand(MatchStore store, OutputWriter output) : DraftSetupCommand(output)
{
    /// <summary>Where to write the match.</summary>
    [Option("--out", Description = "Match file to create.")]
    [Required]
    public string Out { get; set; } = "";

    /// <summary>Built-in map.</summary>
    [Option("--map", Description = "Built-in map (default riverford).")]
    public string Map { get; set; } = "riverford";

    /// <summary>Who plays P1.</summary>
    [Option("--p1", Description = "Who plays P1: llm, human, or bot:random (default llm).")]
    public string P1 { get; set; } = "llm";

    /// <summary>Who plays P2.</summary>
    [Option("--p2", Description = "Who plays P2: llm, human, or bot:random (default bot:random).")]
    public string P2 { get; set; } = "bot:random";

    /// <summary>Match seed.</summary>
    [Option("--seed", Description = "Seed for rule randomness and bots (default 1).")]
    public ulong Seed { get; set; } = 1;

    /// <summary>Overwrite an existing file.</summary>
    [Option("--force", Description = "Overwrite an existing match file.")]
    public bool Force { get; set; }

    /// <inheritdoc />
    protected override int Execute()
    {
        if (File.Exists(Out) && !Force)
        {
            throw new SimException($"'{Out}' already exists. Pass --force to overwrite it.");
        }

        if (!MapLibrary.Names.Contains(Map))
        {
            throw new SimException($"Unknown map '{Map}'. Built-in maps: {string.Join(", ", MapLibrary.Names)}.");
        }

        MatchSetup setup = new(
            Map,
            Seed,
            new Dictionary<Seat, string>
            {
                [Seat.P1] = SeatKind.Parse(P1).Value,
                [Seat.P2] = SeatKind.Parse(P2).Value,
            }.ToImmutableSortedDictionary(),
            AllowedRaces(store.Rules),
            DraftBudgets(),
            StartingCaps());

        using IDisposable fileLock = store.Lock(Out);
        MatchSession session = MatchSession.Create(store.Rules, setup);
        session.AdvanceBots();
        store.Save(Out, session);
        Output.Write(ViewBuilder.Status(session), Format);
        return ExitCodes.Ok;
    }
}
