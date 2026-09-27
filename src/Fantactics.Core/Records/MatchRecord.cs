using System.Collections.Immutable;
using System.Text.Json;
using Fantactics.Core.Serialization;

namespace Fantactics.Core.Records;

/// <summary>A match as setup plus command log (Simulation §5). Replaying it reproduces every state.</summary>
/// <param name="FormatVersion">Record format version.</param>
/// <param name="RulesVersion">Rules code version that produced it.</param>
/// <param name="RulesConfigHash">Hash of the rules config that produced it.</param>
/// <param name="Setup">Starting setup.</param>
/// <param name="Commands">Accepted commands in order.</param>
public sealed record MatchRecord(
    int FormatVersion,
    string RulesVersion,
    string RulesConfigHash,
    MatchSetup Setup,
    ImmutableArray<RecordedCommand> Commands)
{
    /// <summary>The current record format.</summary>
    public const int CurrentFormatVersion = 1;

    /// <summary>Parses a record from JSON.</summary>
    /// <exception cref="JsonException">The JSON is malformed.</exception>
    public static MatchRecord FromJson(string json) =>
        JsonSerializer.Deserialize<MatchRecord>(json, CoreJson.Options)
            ?? throw new JsonException("Match record is empty.");

    /// <summary>Serializes the record as indented JSON.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, CoreJson.IndentedOptions);
}
