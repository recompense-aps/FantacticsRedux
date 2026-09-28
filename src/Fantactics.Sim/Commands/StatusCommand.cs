using Fantactics.Core;
using Fantactics.Core.Engine;
using Fantactics.Sim.Matches;
using Fantactics.Sim.Output;
using Fantactics.Sim.Views;
using McMaster.Extensions.CommandLineUtils;

namespace Fantactics.Sim.Commands;

/// <summary>
/// Prints the phase, turn, and which seats owe a decision. Shows no hidden information. With <c>--wait-for</c>, it
/// blocks until that seat owes a decision or the match ends, so players in self-play can wait for their turn.
/// </summary>
/// <param name="store">Match file store.</param>
/// <param name="output">Where results are printed.</param>
[Command("status", Description = "Show the phase, turn, and which seats owe a decision.")]
public sealed class StatusCommand(MatchStore store, OutputWriter output) : MatchCommand(output)
{
    private static readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>Seat to wait for.</summary>
    [Option("--wait-for", Description = "Block until this seat owes a decision or the match ends.")]
    public Seat? WaitFor { get; set; }

    /// <summary>Longest time to wait.</summary>
    [Option("--timeout", Description = "Seconds to wait with --wait-for before giving up (default 240).")]
    public int Timeout { get; set; } = 240;

    /// <inheritdoc />
    protected override int Execute()
    {
        MatchSession session = store.Load(MatchFile);
        DateTime deadline = DateTime.UtcNow.AddSeconds(Timeout);
        while (WaitFor is Seat seat
            && session.State.Outcome is null
            && GameEngine.PendingDecisionFor(session.State, seat) is null)
        {
            if (DateTime.UtcNow > deadline)
            {
                return Fail("timeout", $"{seat} still doesn't owe a decision after {Timeout}s.", ExitCodes.NotYourDecision);
            }

            Thread.Sleep(_pollInterval);
            session = store.Load(MatchFile);
        }

        Output.Write(ViewBuilder.Status(session), Format);
        return ExitCodes.Ok;
    }
}
