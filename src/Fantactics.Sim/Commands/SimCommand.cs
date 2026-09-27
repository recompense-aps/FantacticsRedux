using Fantactics.Sim.Output;
using Fantactics.Sim.Views;
using McMaster.Extensions.CommandLineUtils;

namespace Fantactics.Sim.Commands;

/// <summary>Base for subcommands: the <c>--format</c> option and uniform error reporting.</summary>
/// <param name="output">Where results are printed.</param>
public abstract class SimCommand(OutputWriter output)
{
    /// <summary>Output format.</summary>
    [Option("--format", Description = "Output format: text (default), toon (for LLM seats), or json.")]
    public OutputFormat Format { get; set; } = OutputFormat.Text;

    /// <summary>Where results are printed.</summary>
    protected OutputWriter Output => output;

    /// <summary>Runs the command, turning <see cref="SimException"/> into an error result and exit code.</summary>
    public int OnExecute()
    {
        try
        {
            return Execute();
        }
        catch (SimException ex)
        {
            return Fail(ex.ExitCode == ExitCodes.RuleViolation ? "bad-orders" : "error", ex.Message, ex.ExitCode);
        }
    }

    /// <summary>The command's work; returns the exit code.</summary>
    protected abstract int Execute();

    /// <summary>Prints an error result and returns <paramref name="exitCode"/>.</summary>
    protected int Fail(string code, string message, int exitCode)
    {
        Output.Write(new ErrorResult("error", code, message), Format);
        return exitCode;
    }
}
