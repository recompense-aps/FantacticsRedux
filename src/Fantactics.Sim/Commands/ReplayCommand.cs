using Fantactics.Core;
using Fantactics.Core.State;
using Fantactics.Sim.Matches;
using Fantactics.Sim.Output;
using Fantactics.Sim.Views;
using McMaster.Extensions.CommandLineUtils;

namespace Fantactics.Sim.Commands;

/// <summary>
/// Prints the full match history once it's over, including each command's note, so players can review it.
/// Refused while the match is running, because notes and hidden orders would leak.
/// </summary>
/// <param name="store">Match file store.</param>
/// <param name="output">Where results are printed.</param>
[Command("replay", Description = "Show the full match history, with notes; only after the match ends.")]
public sealed class ReplayCommand(MatchStore store, OutputWriter output) : MatchCommand(output)
{
    /// <summary>Whose unit names to use.</summary>
    [Option("--as", Description = "Name units from this seat's point of view (default P1).")]
    public Seat As { get; set; } = Seat.P1;

    /// <inheritdoc />
    protected override int Execute()
    {
        MatchSession session = store.Load(MatchFile);
        if (session.State.Phase != Phase.Over)
        {
            return Fail("match-running", "Replay is only available after the match ends.", ExitCodes.NotYourDecision);
        }

        IEnumerable<EventLine> notes = session.ToRecord().Commands
            .Where(command => !string.IsNullOrWhiteSpace(command.Note))
            .Select(command => new EventLine(command.Seq, 0, "note", command.Seat.ToString(), "", command.Note ?? ""));
        List<EventLine> lines = [.. EventFormatter.Format(session.Events, session.HandlesFor(As)), .. notes];
        Output.Write(new LogView([.. lines.OrderBy(line => line.Seq).ThenBy(line => line.Type == "note" ? 0 : 1)]), Format);
        return ExitCodes.Ok;
    }
}
