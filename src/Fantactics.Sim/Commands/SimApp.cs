using McMaster.Extensions.CommandLineUtils;

namespace Fantactics.Sim.Commands;

/// <summary>The root <c>fantactics-sim</c> command. It only groups the subcommands.</summary>
[Command("fantactics-sim", Description = "Play Fantactics matches headlessly: LLM and human seats, bots, tournaments.")]
[Subcommand(
    typeof(NewCommand),
    typeof(StatusCommand),
    typeof(ViewCommand),
    typeof(LegalCommand),
    typeof(ActCommand),
    typeof(LogCommand),
    typeof(ReplayCommand),
    typeof(RunCommand))]
public sealed class SimApp
{
    /// <summary>Shows help when no subcommand is given.</summary>
    public int OnExecute(CommandLineApplication app)
    {
        app.ShowHelp();
        return ExitCodes.Error;
    }
}
