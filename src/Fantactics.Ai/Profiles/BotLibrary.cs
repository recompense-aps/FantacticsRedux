using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Fantactics.Core.Rules;
using Fantactics.Core.Serialization;

namespace Fantactics.Ai.Profiles;

/// <summary>
/// The built-in bots: <c>random</c> plus the personality profiles embedded from <c>Profiles/Data</c>. A bot is named
/// <c>profile</c> or <c>profile@difficulty</c>, e.g. <c>warden@easy</c>.
/// </summary>
public static class BotLibrary
{
    /// <summary>The fuzzing bot, which has no profile.</summary>
    public const string RandomBot = "random";

    private const string ResourcePrefix = "Fantactics.Ai.Profiles.Data.";
    private const string DifficultyPrefix = "difficulty-";

    private static readonly Lazy<ImmutableSortedDictionary<string, JsonObject>> _profiles =
        new(() => LoadDocuments(name => !name.StartsWith(DifficultyPrefix, StringComparison.Ordinal)));

    private static readonly Lazy<ImmutableSortedDictionary<string, JsonObject>> _difficulties =
        new(() => LoadDocuments(name => name.StartsWith(DifficultyPrefix, StringComparison.Ordinal), DifficultyPrefix));

    /// <summary>Bot names: <see cref="RandomBot"/> and every profile.</summary>
    public static IReadOnlyList<string> Names => [RandomBot, .. _profiles.Value.Keys];

    /// <summary>Difficulty preset names, easiest first.</summary>
    public static IReadOnlyList<string> Difficulties { get; } =
        ["novice", "easy", "normal", "hard", "expert", "master"];

    /// <summary>Whether <paramref name="spec"/> names a built-in bot (and a valid difficulty, if given).</summary>
    public static bool IsKnown(string spec)
    {
        (string name, string? difficulty) = Split(spec);
        return name == RandomBot
            ? difficulty is null
            : _profiles.Value.ContainsKey(name) && (difficulty is null || _difficulties.Value.ContainsKey(difficulty));
    }

    /// <summary>Resolves a built-in profile.</summary>
    /// <param name="spec"><c>profile</c> or <c>profile@difficulty</c>.</param>
    /// <exception cref="ArgumentException">The profile or difficulty is unknown.</exception>
    public static BotProfile Profile(string spec)
    {
        (string name, string? difficulty) = Split(spec);
        if (!_profiles.Value.TryGetValue(name, out JsonObject? document))
        {
            throw new ArgumentException($"Unknown bot profile '{name}'.", nameof(spec));
        }

        return FromDocument(document, difficulty);
    }

    /// <summary>Parses a profile file, e.g. a candidate from tuning.</summary>
    /// <param name="json">The profile JSON (same shape as the built-in profiles).</param>
    /// <param name="difficulty">Overrides the profile's own difficulty preset.</param>
    /// <exception cref="ArgumentException">The JSON isn't a valid profile or names an unknown difficulty.</exception>
    public static BotProfile FromJson(string json, string? difficulty = null) =>
        JsonNode.Parse(json) is JsonObject document
            ? FromDocument(document, difficulty)
            : throw new ArgumentException("A bot profile must be a JSON object.", nameof(json));

    /// <summary>Creates a built-in bot.</summary>
    /// <param name="spec">Bot name, optionally with <c>@difficulty</c>.</param>
    /// <param name="rules">Rules the match uses (bots simulate with them).</param>
    /// <param name="seed">Seed for any randomness in the bot.</param>
    /// <exception cref="ArgumentException">The bot is unknown.</exception>
    public static IPlayerAgent Create(string spec, RulesConfig rules, int seed) =>
        spec == RandomBot ? new RandomAgent(seed) : new TacticalAgent(Profile(spec), rules, seed);

    private static (string Name, string? Difficulty) Split(string spec)
    {
        int at = spec.IndexOf('@');
        return at < 0 ? (spec, null) : (spec[..at], spec[(at + 1)..]);
    }

    private static BotProfile FromDocument(JsonObject document, string? difficultyOverride)
    {
        string name = document["name"]?.GetValue<string>()
            ?? throw new ArgumentException("A bot profile needs a name.");
        string difficulty = difficultyOverride ?? document["difficulty"]?.GetValue<string>() ?? "normal";
        if (!_difficulties.Value.TryGetValue(difficulty, out JsonObject? preset))
        {
            throw new ArgumentException($"Unknown difficulty '{difficulty}'.");
        }

        JsonObject skill = Merge(preset, document["skill"] as JsonObject);
        try
        {
            return new BotProfile(
                name,
                document["description"]?.GetValue<string>() ?? "",
                difficulty,
                skill.Deserialize<SkillSettings>(CoreJson.Options) ?? new SkillSettings(),
                document["style"]?.Deserialize<StyleWeights>(CoreJson.Options) ?? new StyleWeights());
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Bot profile '{name}' is invalid: {ex.Message}", ex);
        }
    }

    /// <summary>A copy of <paramref name="baseline"/> with <paramref name="overrides"/> merged in.</summary>
    private static JsonObject Merge(JsonObject baseline, JsonObject? overrides)
    {
        JsonObject merged = (JsonObject)baseline.DeepClone();
        foreach ((string key, JsonNode? value) in overrides ?? [])
        {
            merged[key] = value is JsonObject child && merged[key] is JsonObject existing
                ? Merge(existing, child)
                : value?.DeepClone();
        }

        return merged;
    }

    private static ImmutableSortedDictionary<string, JsonObject> LoadDocuments(
        Func<string, bool> include,
        string stripPrefix = "")
    {
        Assembly assembly = typeof(BotLibrary).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(resource => resource.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                && resource.EndsWith(".json", StringComparison.Ordinal))
            .Select(resource => (Resource: resource, Name: resource[ResourcePrefix.Length..^".json".Length]))
            .Where(entry => include(entry.Name))
            .ToImmutableSortedDictionary(
                entry => entry.Name[stripPrefix.Length..],
                entry => Load(assembly, entry.Resource),
                StringComparer.Ordinal);
    }

    private static JsonObject Load(Assembly assembly, string resource)
    {
        using Stream stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Missing embedded resource {resource}.");
        return JsonNode.Parse(stream) as JsonObject
            ?? throw new InvalidOperationException($"Embedded profile {resource} is not a JSON object.");
    }
}
