using System.Text.RegularExpressions;
using Fantactics.Core.Engine;
using Fantactics.Core.Rules;

namespace Fantactics.Client.Logic.Board;

/// <summary>How unit types are described in the draft and in tooltips.</summary>
public static partial class UnitText
{
    /// <summary>Words a title keeps lowercase after its first word.</summary>
    private static readonly HashSet<string> _minorWords = ["A", "An", "And", "Of", "The", "To", "In", "On"];

    /// <summary>Race, classes, and uniqueness, e.g. <c>Elves · Ranged · unique</c>.</summary>
    public static string Tags(DraftUnitOption unit) =>
        string.Join(" · ", [unit.Race, .. unit.Classes, .. unit.Unique ? ["unique"] : Array.Empty<string>()]);

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
