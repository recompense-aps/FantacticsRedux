using System.Text.Json;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Core.Serialization;

/// <summary>Reads and writes whole game states (save snapshots), in the same JSON <see cref="Engine.StateHash"/> hashes.</summary>
public static class GameStateJson
{
    /// <summary>Serializes <paramref name="state"/> as indented JSON. The rules config is not included.</summary>
    public static string Write(GameState state) => JsonSerializer.Serialize(state, CoreJson.IndentedOptions);

    /// <summary>Parses a state and attaches <paramref name="rules"/>, which the JSON doesn't carry.</summary>
    /// <exception cref="JsonException">The JSON is malformed.</exception>
    public static GameState Read(string json, RulesConfig rules) => Attach(
        JsonSerializer.Deserialize<GameState>(json, CoreJson.Options)
            ?? throw new JsonException("Game state is empty."),
        rules);

    /// <summary>Attaches <paramref name="rules"/> to a state read from JSON (its rules are <c>null</c> until then).</summary>
    public static GameState Attach(GameState state, RulesConfig rules) => state with { Rules = rules };
}
