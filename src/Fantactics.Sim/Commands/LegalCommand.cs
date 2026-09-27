using Fantactics.Sim.Matches;
using Fantactics.Sim.Output;
using Fantactics.Sim.Views;
using McMaster.Extensions.CommandLineUtils;

namespace Fantactics.Sim.Commands;

/// <summary>Lists the options for the seat's pending decision and how to submit them.</summary>
/// <param name="store">Match file store.</param>
/// <param name="output">Where results are printed.</param>
[Command("legal", Description = "List the options for a seat's pending decision.")]
public sealed class LegalCommand(MatchStore store, OutputWriter output) : SeatCommand(output)
{
    /// <summary>Only list this unit's move destinations.</summary>
    [Option("--unit", Description = "During movement, only list this unit's destinations.")]
    public string? Unit { get; set; }

    /// <inheritdoc />
    protected override int Execute()
    {
        LegalView? legal = LegalBuilder.Build(store.Load(MatchFile), As, Unit);
        if (legal is null)
        {
            return Fail("not-your-decision", $"{As} doesn't owe a decision right now.", ExitCodes.NotYourDecision);
        }

        Output.Write(legal, Format);
        return ExitCodes.Ok;
    }
}
