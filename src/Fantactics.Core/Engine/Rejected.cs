namespace Fantactics.Core.Engine;

/// <summary>The command was illegal; the state is unchanged.</summary>
/// <param name="Violation">Why it was rejected.</param>
public sealed record Rejected(RuleViolation Violation) : ApplyResult;
