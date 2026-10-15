using System.Text.RegularExpressions;
using Fantactics.Core.Engine;
using Fantactics.Core.Rules;

namespace Fantactics.Client.Logic.Board;

/// <summary>How unit types are described in the draft and in tooltips.</summary>
public static partial class UnitText
{
    /// <summary>Words a title keeps lowercase after its first word.</summary>
    private static readonly HashSet<string> _minorWords = ["A", "An", "And", "Of", "The", "To", "In", "On"];

    /// <summary>
    /// Race, classes, role, and uniqueness, e.g. <c>Elves · Ranged</c> or <c>Goblins · Leader · War Lord · unique</c>.
    /// </summary>
    public static string Tags(DraftUnitOption unit) => string.Join(" · ", [
        unit.Race,
        .. unit.Classes,
        .. unit.Role is string role ? [role] : Array.Empty<string>(),
        .. unit.Unique ? ["unique"] : Array.Empty<string>(),
    ]);

    /// <summary>
    /// A unit type's short name (RacesAndUnits §2.5): a named character's name, e.g. <c>Grukk</c>, or the type in
    /// words, e.g. <c>Wolf Rider</c>.
    /// </summary>
    public static string Name(RulesConfig rules, string type) =>
        rules.Units.TryGetValue(type, out UnitDefinition? unit) && unit.Name is string name ? name : Words(type);

    /// <summary>
    /// A unit type's full name: a named character's name and epithet, e.g. <c>Grukk Skullsplitter</c>, or the type
    /// in words.
    /// </summary>
    public static string FullName(RulesConfig rules, string type) =>
        rules.Units.TryGetValue(type, out UnitDefinition? unit) && unit.FullName is string name ? name : Words(type);

    /// <summary>A draft option's short name (see <see cref="Name(RulesConfig, string)"/>).</summary>
    public static string Name(DraftUnitOption unit) => unit.Name ?? Words(unit.Type);

    /// <summary>A draft option's full name (see <see cref="FullName(RulesConfig, string)"/>).</summary>
    public static string FullName(DraftUnitOption unit) => unit.FullName ?? Words(unit.Type);

    /// <summary>Combat stats, e.g. <c>HP 7 · ATK 4 · DEF 1 · MOV 4 · RNG 2–3 · INI 5</c>.</summary>
    public static string Stats(UnitDefinition unit) => $"HP {unit.Hp} · {CombatStats(unit)}";

    /// <summary>Stats besides HP, e.g. <c>ATK 4 · DEF 1 · MOV 4 · RNG 2–3 · INI 5</c>.</summary>
    public static string CombatStats(UnitDefinition unit)
    {
        string range = unit.MinRange == unit.MaxRange ? $"{unit.MaxRange}" : $"{unit.MinRange}–{unit.MaxRange}";
        return $"ATK {unit.Attack} · DEF {unit.Defense} · MOV {unit.Movement} · RNG {range} · INI {unit.Initiative}";
    }

    /// <summary>Abilities and traits in words, e.g. <c>Pinning Shot; Forest Stride, Slippery</c>, or empty.</summary>
    public static string Features(UnitDefinition unit)
    {
        string abilities = string.Join(", ", unit.Abilities.Select(Words));
        string traits = string.Join(", ", unit.Traits.Keys.Select(Words));
        return string.Join("; ", new[] { abilities, traits }.Where(part => part.Length > 0));
    }

    /// <summary>
    /// Splits an identifier into words, as a title: <c>PinningShot</c> → <c>Pinning Shot</c>,
    /// <c>OutOfTheCaves</c> → <c>Out of the Caves</c>.
    /// </summary>
    public static string Words(string identifier) => string.Join(' ', CamelBoundary()
        .Split(identifier)
        .Select((word, index) => index > 0 && _minorWords.Contains(word) ? word.ToLowerInvariant() : word));

    [GeneratedRegex("(?<=[a-z])(?=[A-Z])")]
    private static partial Regex CamelBoundary();
}
