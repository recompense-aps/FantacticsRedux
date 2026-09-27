namespace Fantactics.Sim.Output;

/// <summary>How commands print their results (Simulation §6.2).</summary>
public enum OutputFormat
{
    /// <summary>Aligned text for humans at a terminal.</summary>
    Text,

    /// <summary>Token-Oriented Object Notation, for LLM seats.</summary>
    Toon,

    /// <summary>Compact JSON, for scripts and tests.</summary>
    Json,
}
