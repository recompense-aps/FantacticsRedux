using System.ComponentModel.DataAnnotations;
using Fantactics.Core;
using Fantactics.Sim.Output;
using McMaster.Extensions.CommandLineUtils;

namespace Fantactics.Sim.Commands;

/// <summary>Base for subcommands that act or look from one seat's point of view.</summary>
/// <param name="output">Where results are printed.</param>
public abstract class SeatCommand(OutputWriter output) : MatchCommand(output)
{
    /// <summary>The seat.</summary>
    [Option("--as", Description = "Seat to act or look as: P1 to P4.")]
    [Required]
    public Seat As { get; set; }
}
