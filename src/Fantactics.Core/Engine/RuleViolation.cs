namespace Fantactics.Core.Engine;

/// <summary>Why a command was rejected.</summary>
/// <param name="Code">Stable, machine-readable code (e.g. <c>out-of-range</c>).</param>
/// <param name="Message">Readable explanation.</param>
public sealed record RuleViolation(string Code, string Message);
