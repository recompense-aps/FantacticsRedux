namespace Fantactics.Core.Engine;

/// <summary>A unit type available in the draft.</summary>
/// <param name="Type">Unit type identifier.</param>
/// <param name="Cost">Draft cost.</param>
/// <param name="Unique">At most one per army.</param>
public sealed record DraftUnitOption(string Type, int Cost, bool Unique);
