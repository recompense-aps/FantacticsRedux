using System.Text.Json;

namespace Fantactics.Client.Logic.Settings;

/// <summary>Player preferences, kept as JSON in the user's data folder.</summary>
/// <param name="Speed">Animation speed multiplier; 0 means instant.</param>
/// <param name="AutoSkip">Units with nothing meaningful to do wait without asking.</param>
/// <param name="Curtain">Hide the board between hotseat players.</param>
/// <param name="ShowHash">Show the state hash in the debug panel.</param>
public sealed record ClientSettings(double Speed = 1, bool AutoSkip = true, bool Curtain = true, bool ShowHash = false)
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>Reads settings from <paramref name="path"/>, or the defaults if it's missing or unreadable.</summary>
    public static ClientSettings Load(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<ClientSettings>(File.ReadAllText(path), _json) ?? new() : new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    /// <summary>Writes the settings to <paramref name="path"/>.</summary>
    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, _json));
}
