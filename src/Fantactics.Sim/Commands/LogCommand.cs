using System.Collections.Immutable;
using Fantactics.Sim.Matches;
using Fantactics.Sim.Output;
using Fantactics.Sim.Views;
using McMaster.Extensions.CommandLineUtils;

namespace Fantactics.Sim.Commands;

/// <summary>Prints the event history from one seat's point of view.</summary>
/// <param name="store">Match file store.</param>
/// <param name="output">Where results are printed.</param>
[Command("log", Description = "Show the event history from a seat's point of view.")]
public sealed class LogCommand(MatchStore store, OutputWriter output) : SeatCommand(output)
{
    /// <summary>Only show the most recent events.</summary>
    [Option("--last", Description = "Only show the last N events.")]
    public int? Last { get; set; }

    /// <inheritdoc />
    protected override int Execute()
    {
        MatchSession session = store.Load(MatchFile);
        ImmutableArray<EventLine> events = EventFormatter.Format(session.Events, session.HandlesFor(As));
        if (Last is int last && last < events.Length)
        {
            events = events[^last..];
        }

        Output.Write(new LogView(events), Format);
        return ExitCodes.Ok;
    }
}
