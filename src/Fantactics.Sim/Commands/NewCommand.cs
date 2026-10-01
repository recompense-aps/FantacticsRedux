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

    /// <summary>Who plays P3, if the match has a third seat.</summary>
    [Option("--p3", Description = "Who plays P3, on a map with room for it (default: no P3).")]
    public string? P3 { get; set; }

    /// <summary>Who plays P4, if the match has a fourth seat.</summary>
    [Option("--p4", Description = "Who plays P4, on a map with room for it (default: no P4).")]
    public string? P4 { get; set; }

    /// <summary>Each seat's team.</summary>
    [Option("--teams", Description = "Each seat's team in seat order, e.g. 1,2,1,2 (default: all on their own).")]
    public string? Teams { get; set; }

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

        if (P4 is not null && P3 is null)
        {
            throw new SimException("Give --p3 before --p4: seats are filled in order.");
        }

        ImmutableSortedDictionary<Seat, string> seats = new Dictionary<Seat, string?>
        {
            [Seat.P1] = P1,
            [Seat.P2] = P2,
            [Seat.P3] = P3,
            [Seat.P4] = P4,
        }
            .Where(pair => pair.Value is not null)
            .ToImmutableSortedDictionary(pair => pair.Key, pair => SeatKind.Parse(pair.Value!).Value);
        List<Seat> seatList = [.. seats.Keys];
        MatchSetup setup = new(
            Map,
            Seed,
            seats,
            AllowedRaces(store.Rules, seatList),
            DraftBudgets(seatList),
            StartingCaps(seatList),
            ParseTeams(Teams, seatList));

        using IDisposable fileLock = store.Lock(Out);
        MatchSession session;
        try
        {
            session = MatchSession.Create(store.Rules, setup);
        }
        catch (ArgumentException ex)
        {
            throw new SimException(ex.Message);
        }

        session.AdvanceBots();
        store.Save(Out, session);
        Output.Write(ViewBuilder.Status(session), Format);
        return ExitCodes.Ok;
    }

    /// <summary>Parses <c>--teams</c>: one positive team number per seat, in seat order.</summary>
    /// <exception cref="SimException">The list is malformed or doesn't name a team for every seat.</exception>
    private static ImmutableSortedDictionary<Seat, int>? ParseTeams(string? value, List<Seat> seats)
    {
        if (value is null)
        {
            return null;
        }

        string[] parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != seats.Count || parts.Any(part => !int.TryParse(part, out int team) || team <= 0))
        {
            throw new SimException(
                "--teams needs one team number from 1 per seat, e.g. "
                + $"{string.Join(',', seats.Select(seat => seat.OwnTeam()))}.");
        }

        return seats
            .Zip(parts, (seat, part) => KeyValuePair.Create(seat, int.Parse(part)))
            .ToImmutableSortedDictionary();
    }
}
