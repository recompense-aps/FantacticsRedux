using System.Text.Encodings.Web;
using System.Text.Json;
using Fantactics.Core.Serialization;
using McMaster.Extensions.CommandLineUtils;
using ToonFormat;

namespace Fantactics.Sim.Output;

/// <summary>
/// Prints result DTOs. One model, three encoders (Simulation §6.2): the DTO is serialized to JSON, which is printed
/// as is or converted to TOON; text is rendered from the same DTO.
/// </summary>
/// <param name="console">Console to write to.</param>
public sealed class OutputWriter(IConsole console)
{
    // Output is read by people and LLMs, not embedded in HTML, so skip escaping characters like apostrophes.
    private static readonly JsonSerializerOptions _options = new(CoreJson.Options)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Prints <paramref name="value"/> in <paramref name="format"/>.</summary>
    public void Write(object value, OutputFormat format)
    {
        string json = JsonSerializer.Serialize(value, value.GetType(), _options);
        string output = format switch
        {
            OutputFormat.Json => json,
            OutputFormat.Toon => Toon.FromJson(json),
            _ => TextRenderer.Render(value),
        };
        console.Out.WriteLine(output);
    }
}
