using System.Collections.Immutable;
using System.Text.Json;
using Fantactics.Core.Rules;
using Fantactics.Core.Serialization;
using Fantactics.Core.State;

namespace Fantactics.Core.Records;

/// <summary>
/// A match as setup plus command log (Simulation §5), which doubles as the save file (TechnicalDesign §4). Replaying
/// it reproduces every state; <see cref="MatchResume"/> decides where play continues from.
/// </summary>
/// <param name="FormatVersion">Record format version.</param>
/// <param name="RulesVersion">Rules code version that produced it.</param>
/// <param name="RulesConfigHash">Hash of the rules config that produced it.</param>
/// <param name="Setup">Starting setup.</param>
/// <param name="Commands">Accepted commands in order.</param>
/// <param name="Start">
/// The position the commands start from, when it isn't the setup's starting state (a match continued from a
/// snapshot). <c>null</c> for matches played from the start.
/// </param>
/// <param name="Snapshot">
/// The state after the last command, written with every save. Play resumes from it when the history no longer
/// replays (the rules changed) or when it was edited by hand.
/// </param>
public sealed record MatchRecord(
    int FormatVersion,
    string RulesVersion,
    string RulesConfigHash,
    MatchSetup Setup,
    ImmutableArray<RecordedCommand> Commands,
    GameState? Start = null,
    GameState? Snapshot = null)
{
    /// <summary>The current record format: 2 added <see cref="Start"/> and <see cref="Snapshot"/>.</summary>
    public const int CurrentFormatVersion = 2;

    /// <summary>Parses a record from JSON, attaching <paramref name="rules"/> to its states.</summary>
    /// <exception cref="JsonException">The JSON is malformed.</exception>
    public static MatchRecord FromJson(string json, RulesConfig rules)
    {
        MatchRecord record = JsonSerializer.Deserialize<MatchRecord>(json, CoreJson.Options)
            ?? throw new JsonException("Match record is empty.");
        return record with
        {
            Start = record.Start is GameState start ? GameStateJson.Attach(start, rules) : null,
            Snapshot = record.Snapshot is GameState snapshot ? GameStateJson.Attach(snapshot, rules) : null,
        };
    }

    /// <summary>Serializes the record as indented JSON.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, CoreJson.IndentedOptions);

    /// <summary>The state the commands start from.</summary>
    public GameState StartState(RulesConfig rules) => Start ?? Setup.CreateInitialState(rules);

    /// <summary>The record cut after command <paramref name="seq"/> (0 keeps none), without a snapshot.</summary>
    public MatchRecord Truncated(int seq) => this with
    {
        Commands = [.. Commands.Where(command => command.Seq <= seq)],
        Snapshot = null,
    };
}
