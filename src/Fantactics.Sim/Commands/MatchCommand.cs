using System.ComponentModel.DataAnnotations;
using Fantactics.Sim.Output;
using McMaster.Extensions.CommandLineUtils;

namespace Fantactics.Sim.Commands;

/// <summary>Base for subcommands that operate on a match file.</summary>
/// <param name="output">Where results are printed.</param>
public abstract class MatchCommand(OutputWriter output) : SimCommand(output)
{
    /// <summary>Path of the match file.</summary>
    [Argument(0, "match", Description = "Match file (JSON record).")]
    [Required]
    public string MatchFile { get; set; } = "";
}
