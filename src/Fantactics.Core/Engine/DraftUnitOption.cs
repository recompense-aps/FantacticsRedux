using System.Collections.Immutable;

namespace Fantactics.Core.Engine;

/// <summary>A unit type available in the draft.</summary>
/// <param name="Type">Unit type identifier.</param>
/// <param name="Cost">Draft cost.</param>
/// <param name="Unique">At most one per army.</param>
/// <param name="Race">The unit's race.</param>
/// <param name="Classes">The unit's class tags (RacesAndUnits §2.4).</param>
/// <param name="Name">A named character's short name, e.g. <c>Grukk</c>, or <c>null</c> for a generic unit.</param>
/// <param name="FullName">The name with its epithet, e.g. <c>Grukk Skullsplitter</c>, or <c>null</c>.</param>
/// <param name="Role">What the character is, e.g. <c>War Lord</c>, or <c>null</c> (RacesAndUnits §2.5).</param>
public sealed record DraftUnitOption(
    string Type,
    int Cost,
    bool Unique,
    string Race,
    ImmutableSortedSet<string> Classes,
    string? Name = null,
    string? FullName = null,
    string? Role = null);
