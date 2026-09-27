using Fantactics.Sim.Matches;
using Fantactics.Sim.Output;
using Fantactics.Sim.Views;
using McMaster.Extensions.CommandLineUtils;

namespace Fantactics.Sim.Commands;

/// <summary>Prints what one seat can see: map, units, reserve, pending decision, and recent events.</summary>
/// <param name="store">Match file store.</param>
/// <param name="output">Where results are printed.</param>
[Command("view", Description = "Show what a seat can see.")]
public sealed class ViewCommand(MatchStore store, OutputWriter output) : SeatCommand(output)
{
    /// <inheritdoc />
    protected override int Execute()
    {
        Output.Write(ViewBuilder.Build(store.Load(MatchFile), As, includeMap: true), Format);
        return ExitCodes.Ok;
    }
}
