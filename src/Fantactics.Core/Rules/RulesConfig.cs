using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fantactics.Core.Maps;
using Fantactics.Core.Serialization;

namespace Fantactics.Core.Rules;

/// <summary>
/// Unit stats and tunable rule numbers, loaded from JSON so tournaments can compare variants (Simulation §2).
/// </summary>
/// <param name="DraftBudget">Draft points per player (GameDesign §4.4).</param>
/// <param name="StartingCap">Maximum Cost deployed at the start.</param>
/// <param name="DeployColumns">Width of each deploy zone in columns.</param>
/// <param name="CommandPerTurn">Command gained each turn.</param>
/// <param name="CommandStartTurn">First turn that grants Command.</param>
/// <param name="MaxArrivalsPerTurn">Reserve arrivals allowed per player per turn.</param>
/// <param name="HeldBonus">Initiative bonus for holding (GameDesign §4.1).</param>
/// <param name="BracedBonus">Initiative bonus for a braced unit.</param>
/// <param name="SupportCap">Maximum Support bonus (GameDesign §4.3).</param>
/// <param name="RoutPercent">A player whose army value falls below this percent of the draft budget routs.</param>
/// <param name="TurnLimit">Last turn of a Deathmatch.</param>
/// <param name="MaxClashStrikes">Strikes after which a clash ends with no winner.</param>
/// <param name="Terrain">Rules per terrain type.</param>
/// <param name="Races">Race definitions by identifier.</param>
/// <param name="Units">Unit definitions by type identifier.</param>
/// <param name="Abilities">Ability tuning by identifier.</param>
public sealed record RulesConfig(
    int DraftBudget,
    int StartingCap,
    int DeployColumns,
    int CommandPerTurn,
    int CommandStartTurn,
    int MaxArrivalsPerTurn,
    int HeldBonus,
    int BracedBonus,
    int SupportCap,
    int RoutPercent,
    int TurnLimit,
    int MaxClashStrikes,
    ImmutableSortedDictionary<Terrain, TerrainDefinition> Terrain,
    ImmutableSortedDictionary<string, RaceDefinition> Races,
    ImmutableSortedDictionary<string, UnitDefinition> Units,
    ImmutableSortedDictionary<string, AbilityDefinition> Abilities)
{
    private const string DefaultResource = "Fantactics.Core.Rules.Data.mvp-rules.json";

    private static readonly Lazy<RulesConfig> _default = new(LoadDefault);

    /// <summary>The MVP rules shipped with the game.</summary>
    public static RulesConfig Default => _default.Value;

    /// <summary>SHA-256 of the canonical serialization, recorded in match records.</summary>
    [JsonIgnore]
    public string Hash => Convert.ToHexString(SHA256.HashData(CoreJson.SerializeToUtf8Bytes(this))).ToLowerInvariant();

    /// <summary>Parses a rules config from JSON.</summary>
    /// <exception cref="JsonException">The JSON is malformed or incomplete.</exception>
    public static RulesConfig FromJson(string json) =>
        JsonSerializer.Deserialize<RulesConfig>(json, CoreJson.Options)
            ?? throw new JsonException("Rules config is empty.");

    /// <summary>Returns a unit type's traits merged with its race's traits.</summary>
    public ImmutableSortedDictionary<string, int> TraitsOf(string unitType)
    {
        UnitDefinition unit = Units[unitType];
        return Races[unit.Race].Traits.SetItems(unit.Traits);
    }

    private static RulesConfig LoadDefault()
    {
        using Stream stream = typeof(RulesConfig).Assembly.GetManifestResourceStream(DefaultResource)
            ?? throw new InvalidOperationException($"Missing embedded resource {DefaultResource}.");
        using StreamReader reader = new(stream);
        return FromJson(reader.ReadToEnd());
    }
}
