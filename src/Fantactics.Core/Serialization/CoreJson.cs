using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fantactics.Core.Serialization;

/// <summary>The canonical JSON settings for Core types: rules config, commands, events, records, and hashes.</summary>
public static class CoreJson
{
    /// <summary>Serializer options: camelCase names, enums as strings, no indentation.</summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions(indented: false);

    /// <summary>Like <see cref="Options"/>, but indented for files people read.</summary>
    public static JsonSerializerOptions IndentedOptions { get; } = CreateOptions(indented: true);

    /// <summary>Serializes <paramref name="value"/> with <see cref="Options"/>.</summary>
    public static byte[] SerializeToUtf8Bytes<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Options);

    private static JsonSerializerOptions CreateOptions(bool indented)
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = indented,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
