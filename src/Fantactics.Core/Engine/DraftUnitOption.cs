using System.Collections.Immutable;

namespace Fantactics.Core.Engine;

/// <summary>A unit type available in the draft.</summary>
/// <param name="Type">Unit type identifier.</param>
/// <param name="Cost">Draft cost.</param>
/// <param name="Unique">At most one per army.</param>
/// <param name="Race">The unit's race.</param>
/// <param name="Classes">The unit's class tags (RacesAndUnits §2.4).</param>
public sealed record DraftUnitOption(string Type, int Cost, bool Unique, string Race, ImmutableSortedSet<string> Classes);
