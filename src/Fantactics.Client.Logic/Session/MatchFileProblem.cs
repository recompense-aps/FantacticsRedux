using System.Text.Json;
using Fantactics.Core.Maps;
using Fantactics.Core.Records;

namespace Fantactics.Client.Logic.Session;

/// <summary>
/// Why a JSON file can't be loaded as a match, judged from its JSON alone (before reading the record): it isn't a
/// match, it's in a newer format, or its map isn't one this version has.
/// </summary>
public static class MatchFileProblem
{
    /// <summary>The file's JSON isn't a match record.</summary>
    public const string NotAMatch = "not a match file";

    /// <summary>The record is in a format this version doesn't read.</summary>
    public const string Newer = "saved by a newer version of Fantactics";

    /// <summary>The file isn't JSON at all, or is cut short.</summary>
    public const string NotJson = "not valid JSON";

    /// <summary>The problem with <paramref name="root"/>, or <c>null</c> if it looks like a match record.</summary>
    public static string? Of(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("setup", out JsonElement setup)
            || setup.ValueKind != JsonValueKind.Object
            || !setup.TryGetProperty("seats", out JsonElement seats)
            || seats.ValueKind != JsonValueKind.Object)
        {
            return NotAMatch;
        }

        if (root.TryGetProperty("formatVersion", out JsonElement format)
            && format.ValueKind == JsonValueKind.Number
            && format.TryGetInt32(out int version)
            && version > MatchRecord.CurrentFormatVersion)
        {
            return Newer;
        }

        return setup.TryGetProperty("map", out JsonElement map)
            && map.ValueKind == JsonValueKind.String
            && !MapLibrary.Names.Contains(map.GetString())
                ? $"uses the map '{map.GetString()}', which this version doesn't have"
                : null;
    }

    /// <summary>The record's <c>rulesVersion</c>, or <c>null</c> if it has none.</summary>
    public static string? RulesVersionOf(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("rulesVersion", out JsonElement version)
            && version.ValueKind == JsonValueKind.String
                ? version.GetString()
                : null;
}
