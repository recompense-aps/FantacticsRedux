namespace Fantactics.Sim;

/// <summary>Process exit codes (Simulation §6.1).</summary>
public static class ExitCodes
{
    /// <summary>Success.</summary>
    public const int Ok = 0;

    /// <summary>Bad arguments, unreadable file, or another error.</summary>
    public const int Error = 1;

    /// <summary>The command broke a game rule or the order syntax.</summary>
    public const int RuleViolation = 2;

    /// <summary>The seat doesn't owe a decision right now.</summary>
    public const int NotYourDecision = 3;
}
