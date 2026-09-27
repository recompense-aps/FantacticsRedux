namespace Fantactics.Sim;

/// <summary>A user-facing CLI error (bad arguments, unreadable match file, bad order syntax).</summary>
/// <param name="message">What went wrong, written for the person or LLM at the terminal.</param>
/// <param name="exitCode">Exit code to return.</param>
public sealed class SimException(string message, int exitCode = ExitCodes.Error) : Exception(message)
{
    /// <summary>Exit code to return.</summary>
    public int ExitCode { get; } = exitCode;
}
