using Fantactics.Sim.Matches;
using Fantactics.Sim.Output;
using Fantactics.Sim.Views;
using McMaster.Extensions.CommandLineUtils;

namespace Fantactics.Sim.Commands;

/// <summary>Prints the phase, turn, and which seats owe a decision. Shows no hidden information.</summary>
/// <param name="store">Match file store.</param>
/// <param name="output">Where results are printed.</param>
[Command("status", Description = "Show the phase, turn, and which seats owe a decision.")]
public sealed class StatusCommand(MatchStore store, OutputWriter output) : MatchCommand(output)
{
    /// <inheritdoc />
    protected override int Execute()
    {
        Output.Write(ViewBuilder.Status(store.Load(MatchFile)), Format);
        return ExitCodes.Ok;
    }
}
